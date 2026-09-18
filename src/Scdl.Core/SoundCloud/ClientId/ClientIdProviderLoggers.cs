namespace Scdl.Core.SoundCloud.ClientId;

/// <summary>
/// Source generated log methods for <see cref="ClientIdProvider"/>. Keeping them
/// out of the class body means the provider reads as the algorithm it is, and
/// the generator produces cached delegates rather than boxing on every call.
/// </summary>
internal static partial class ClientIdProviderLoggers
{
    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Found {Count} web player bundle(s) to search.")]
    internal static partial void LogBundlesFound(ILogger logger, int count);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Scraped client_id from {Bundle}.")]
    internal static partial void LogClientIdScraped(ILogger logger, string bundle);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Bundle {Bundle} unreachable: {Reason}")]
    internal static partial void LogBundleUnreachable(ILogger logger, string bundle, string reason);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "Using cached client_id.")]
    internal static partial void LogCacheHit(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "client_id cache unreadable: {Reason}")]
    internal static partial void LogCacheUnreadable(ILogger logger, string reason);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "client_id cache not written: {Reason}")]
    internal static partial void LogCacheUnwritable(ILogger logger, string reason);
}
