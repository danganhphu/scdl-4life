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
}
