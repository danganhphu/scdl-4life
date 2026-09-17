using System.Diagnostics;

namespace Scdl.Core.Downloading;

/// <summary>
/// Stream copies an HLS playlist into a container using ffmpeg. Nothing is
/// re-encoded, so the audio lands byte identical to what the CDN served.
/// </summary>
/// <remarks>
/// This is only reached for fragmented MP4 playlists. Plain segment playlists
/// are concatenated in managed code, which is why ffmpeg is an optional
/// dependency rather than a hard one.
/// </remarks>
public sealed partial class FfmpegMuxer(ILogger<FfmpegMuxer> logger) : IMediaMuxer
{
    private readonly ILogger<FfmpegMuxer> _logger = logger;

    private static readonly string[] MuxerBoundArguments = ["-movflags", "+faststart"];

    public bool IsAvailable => Locate() is not null;

    /// <summary>Full path of the ffmpeg executable, or null when it is not on PATH.</summary>
    public static string? Locate()
    {
        var executable = OperatingSystem.IsWindows() ? "ffmpeg.exe" : "ffmpeg";
        var pathVariable = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;

        foreach (var directory in pathVariable.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(directory.Trim('"'), executable);

                if (File.Exists(candidate))
                {
                    return candidate;
                }
            }
            catch (ArgumentException)
            {
                // A malformed PATH entry should not take the whole lookup down.
            }
        }

        return null;
    }

    public async Task MuxAsync(Uri playlistUri, string outputPath, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(playlistUri);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        var ffmpeg = Locate() ??
                     throw new ScdlException(
                         "This track's HLS playlist uses fragmented MP4, which needs a muxer, and ffmpeg is not on PATH. " +
                         "Install it with: winget install Gyan.FFmpeg");

        var startInfo = new ProcessStartInfo(ffmpeg)
        {
            RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true,
        };

        string[] arguments =
        [
            "-hide_banner",
            "-loglevel", "error",
            "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
            "-i", playlistUri.AbsoluteUri,
            "-c", "copy",
        ];

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // faststart is an MP4 muxer option; passing it to another muxer is a
        // hard error in ffmpeg rather than a warning.
        if (Path.GetExtension(outputPath) is ".m4a" or ".mp4")
        {
            foreach (var argument in MuxerBoundArguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(outputPath);

        LogMuxing(outputPath);

        using var process = Process.Start(startInfo) ?? throw new ScdlException("Could not start ffmpeg.");

        // Both pipes must be drained concurrently. Reading one to completion
        // while the other fills its buffer is a classic deadlock.
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode != 0)
        {
            var stderr = await stderrTask.ConfigureAwait(false);

            throw new ScdlException($"ffmpeg failed (exit {process.ExitCode}):{Environment.NewLine}{stderr.Trim()}");
        }
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Stream copying HLS into {OutputPath} with ffmpeg.")]
    private partial void LogMuxing(string outputPath);
}
