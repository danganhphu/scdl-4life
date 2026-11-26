using Scdl.Core.Audio;

namespace Scdl.Core.Downloading;

/// <summary>What a completed download produced, and what it cost in quality.</summary>
public sealed record DownloadResult
{
    public required string FilePath { get; init; }

    public required DownloadSource Source { get; init; }

    public required long Bytes { get; init; }

    /// <summary>The rung taken, or null when the original master was used instead.</summary>
    public AudioRung? Rung { get; init; }

    /// <summary>True when SoundCloud only offered a 30 second preview.</summary>
    public bool IsPreview { get; init; }

    /// <summary>True when an original master was asked for but not offered.</summary>
    public bool FellBackFromOriginal { get; init; }

    /// <summary>
    /// Rungs ranked above the one taken that could not be used, best first, one
    /// sentence each.
    /// </summary>
    /// <remarks>
    /// Carried on the result rather than logged while it happens. The caller
    /// owns the screen during a transfer, and a log line written into a live
    /// progress display overwrites the very text that explains why the file is
    /// smaller than the ladder promised.
    /// </remarks>
    public IReadOnlyList<string> SkippedRungs { get; init; } = [];
}
