using System.Globalization;

namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// Turns the stream ffmpeg writes under <c>-progress</c> into transfer ticks.
/// </summary>
/// <remarks>
/// ffmpeg reports progress as blocks of <c>key=value</c> lines, one key per
/// line, each block closed by <c>progress=continue</c> and the last one by
/// <c>progress=end</c>.
/// <para>
/// The key read here is <c>out_time_us</c>, the position reached in the stream,
/// and not <c>total_size</c>. Measured against ffmpeg 8: muxing five seconds of
/// audio to MP4 emitted a block every half second with <c>total_size</c> stuck
/// at 44, the length of the header, until the trailer was written. The MP4
/// muxer holds every sample to the end, with or without <c>+faststart</c>, so
/// bytes on disk say nothing about how far a mux has got.
/// </para>
/// Split out of <see cref="FfmpegMuxer"/> so the parsing has tests without an
/// ffmpeg on PATH.
/// </remarks>
internal static class FfmpegProgressReader
{
    private const string StreamPositionKey = "out_time_us";

    /// <summary>
    /// Reads <paramref name="output"/> to the end, reporting every position it
    /// carries against <paramref name="duration"/>. The stream is drained even
    /// when nothing is listening, because the pipe filling up would block ffmpeg
    /// rather than just lose the ticks.
    /// </summary>
    /// <param name="output">ffmpeg's progress pipe.</param>
    /// <param name="duration">
    /// How long the assembled audio runs. A tick needs it to mean anything, so a
    /// playlist that declared no duration reports nothing rather than a fraction
    /// of zero.
    /// </param>
    /// <param name="progress">Where the ticks go, if anywhere.</param>
    /// <param name="cancellationToken">Stops reading.</param>
    public static async Task ReadAsync(TextReader output,
                                       TimeSpan duration,
                                       IProgress<TransferProgress>? progress,
                                       CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (progress is null || duration <= TimeSpan.Zero)
        {
            await output.ReadToEndAsync(cancellationToken).ConfigureAwait(false);

            return;
        }

        while (await output.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
        {
            var separator = line.IndexOf('=');

            if (separator < 0 ||
                !line.AsSpan(0, separator).Trim().Equals(StreamPositionKey, StringComparison.Ordinal))
            {
                continue;
            }

            // The first blocks can carry "N/A", before any packet has reached
            // the output, and a non-numeric position is not a tick.
            if (long.TryParse(line.AsSpan(separator + 1).Trim(), CultureInfo.InvariantCulture, out var microseconds))
            {
                progress.Report(TransferProgress.FromStreamTime(TimeSpan.FromMicroseconds(microseconds), duration));
            }
        }
    }
}
