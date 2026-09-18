using System.Diagnostics;
using static Scdl.Core.Downloading.Hls.FfmpegMuxerLoggers;

namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// Stream copies an HLS playlist into a container using ffmpeg. Nothing is
/// re-encoded, so the audio lands byte identical to what the CDN served.
/// </summary>
/// <remarks>
/// Only reached for fragmented MP4 playlists. Plain segment playlists are
/// concatenated in managed code, which is why ffmpeg is an optional dependency
/// rather than a hard one.
/// </remarks>
internal sealed class FfmpegMuxer(ILogger<FfmpegMuxer> logger) : IMediaMuxer
{
    private static readonly string[] BaseArguments =
    [
        "-hide_banner",
        "-loglevel", "error",
        "-protocol_whitelist", "file,http,https,tcp,tls,crypto",
    ];

    /// <summary>faststart is an MP4 muxer option; other muxers reject it outright.</summary>
    private static readonly string[] Mp4Arguments = ["-movflags", "+faststart"];

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

    /// <summary>Stream copies the playlist into <paramref name="outputPath"/> without re-encoding.</summary>
    /// <exception cref="ScdlException">ffmpeg is not on PATH, could not start, or exited non-zero.</exception>
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

        foreach (var argument in BaseArguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.ArgumentList.Add("-i");
        startInfo.ArgumentList.Add(playlistUri.AbsoluteUri);
        startInfo.ArgumentList.Add("-c");
        startInfo.ArgumentList.Add("copy");

        if (Path.GetExtension(outputPath) is ".m4a" or ".mp4")
        {
            foreach (var argument in Mp4Arguments)
            {
                startInfo.ArgumentList.Add(argument);
            }
        }

        startInfo.ArgumentList.Add("-y");
        startInfo.ArgumentList.Add(outputPath);

        LogMuxing(logger, outputPath);

        using var process = Process.Start(startInfo) ?? throw new ScdlException("Could not start ffmpeg.");

        // Both pipes must be drained concurrently. Reading one to completion
        // while the other fills its buffer is a classic deadlock.
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await Task.WhenAll(stderrTask, stdoutTask).ConfigureAwait(false);
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

        if (process.ExitCode is not 0)
        {
            var stderr = await stderrTask.ConfigureAwait(false);

            throw new ScdlException($"ffmpeg failed (exit {process.ExitCode}):{Environment.NewLine}{stderr.Trim()}");
        }
    }
}
