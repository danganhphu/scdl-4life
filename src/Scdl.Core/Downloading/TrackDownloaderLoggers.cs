namespace Scdl.Core.Downloading;

/// <summary>Source generated log methods for <see cref="TrackDownloader"/>.</summary>
internal static partial class TrackDownloaderLoggers
{
    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Track {TrackId} offers no original master; falling back to a transcoding.")]
    internal static partial void LogNoOriginal(ILogger logger, long trackId);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Playlist uses fragmented MP4 across {SegmentCount} segment(s); handing it to the muxer.")]
    internal static partial void LogFragmentedMp4(ILogger logger, int segmentCount);

    // Both of these are Debug rather than Warning on purpose. They fire while a
    // live progress display owns the screen, where a console log line lands on
    // top of the bar and loses most of itself. The user facing copy travels back
    // on DownloadResult.SkippedRungs instead and is rendered once the transfer
    // has finished; these two remain only as diagnostics under --verbose.

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Rung {Preset} ({Kbps} kbps) is fragmented MP4 and ffmpeg is not on PATH; " +
                  "stepping down to the next rung.")]
    internal static partial void LogSteppedDownForMuxer(ILogger logger, string preset, int kbps);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Skipping rung {Preset}: {Reason}")]
    internal static partial void LogRungSkipped(ILogger logger, string preset, string reason);
}
