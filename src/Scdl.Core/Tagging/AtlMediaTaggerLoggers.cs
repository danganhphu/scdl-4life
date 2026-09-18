namespace Scdl.Core.Tagging;

/// <summary>Source generated log methods for <see cref="AtlMediaTagger"/>.</summary>
internal static partial class AtlMediaTaggerLoggers
{
    [LoggerMessage(Level = LogLevel.Warning, Message = "Tagger refused to save {FilePath}.")]
    internal static partial void LogTagRejected(ILogger logger, string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not tag {FilePath}: {Reason}")]
    internal static partial void LogTagFailed(ILogger logger, string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not fetch cover art: {Reason}")]
    internal static partial void LogArtworkFailed(ILogger logger, string reason);
}
