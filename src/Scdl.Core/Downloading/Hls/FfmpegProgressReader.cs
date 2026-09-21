using System.Globalization;

namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// Turns the stream ffmpeg writes under <c>-progress</c> into transfer ticks.
/// </summary>
/// <remarks>
/// ffmpeg reports progress as blocks of <c>key=value</c> lines, one key per
/// line, each block closed by <c>progress=continue</c> and the last one by
/// <c>progress=end</c>. Only <c>total_size</c> is read here; it is how many
/// bytes the muxer has written so far. Split out of <see cref="FfmpegMuxer"/>
/// so the parsing has tests without an ffmpeg on PATH.
/// </remarks>
internal static class FfmpegProgressReader
{
    private const string TotalSizeKey = "total_size";

    /// <summary>
    /// Reads <paramref name="output"/> to the end, reporting every size it
    /// carries. The stream is drained even with no sink attached, because the
    /// pipe filling up would block ffmpeg rather than just lose the ticks.
    /// </summary>
    public static async Task ReadAsync(TextReader output,
                                       IProgress<TransferProgress>? progress,
                                       CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (progress is null)
        {
            await output.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            return;
        }

        while (await output.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var separator = line.IndexOf('=');

            if (separator < 0 || !line.AsSpan(0, separator).Trim().Equals(TotalSizeKey, StringComparison.Ordinal))
            {
                continue;
            }

            // The first blocks can carry "N/A", before any packet has been
            // written, and a non-numeric size is not a tick worth reporting.
            if (long.TryParse(line.AsSpan(separator + 1).Trim(), CultureInfo.InvariantCulture, out var written))
            {
                // The final size is only known once ffmpeg exits, so the mux
                // reports bytes without a denominator, like the segment path.
                progress.Report(new(written, null));
            }
        }
    }
}
