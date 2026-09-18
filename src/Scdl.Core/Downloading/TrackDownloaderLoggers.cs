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

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Rung {Preset} ({Kbps} kbps) is fragmented MP4 and ffmpeg is not on PATH; " +
                  "stepping down to the next rung. Install ffmpeg to get this one.")]
    internal static partial void LogSteppedDownForMuxer(ILogger logger, string preset, int kbps);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Skipping rung {Preset}: {Reason}")]
    internal static partial void LogRungSkipped(ILogger logger, string preset, string reason);
}
