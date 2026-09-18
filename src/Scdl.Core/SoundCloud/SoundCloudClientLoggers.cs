namespace Scdl.Core.SoundCloud;

/// <summary>Source generated log methods for <see cref="SoundCloudClient"/>.</summary>
internal static partial class SoundCloudClientLoggers
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Resolved {Url} to kind '{Kind}'.")]
    internal static partial void LogResolved(ILogger logger, string url, string kind);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Followed short link {ShortUrl} to {ResolvedUrl}.")]
    internal static partial void LogShortLinkFollowed(ILogger logger, string shortUrl, string resolvedUrl);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Hydrating {Count} playlist track stub(s).")]
    internal static partial void LogHydrating(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "No original master for track {TrackId} (HTTP {StatusCode}).")]
    internal static partial void LogOriginalUnavailable(ILogger logger, long trackId, int statusCode);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Rung {Preset} is advertised but not served (HTTP {StatusCode}).")]
    internal static partial void LogRungNotServed(ILogger logger, string preset, int statusCode);
}
