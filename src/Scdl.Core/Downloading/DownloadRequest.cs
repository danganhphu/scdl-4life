using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.Downloading;

/// <summary>One track to fetch, and how the caller wants it fetched.</summary>
public sealed record DownloadRequest
{
    public required Track Track { get; init; }

    public required string OutputDirectory { get; init; }

    public SourcePreference Prefer { get; init; } = SourcePreference.Original;

    /// <summary>Forces a specific preset instead of the top of the ladder.</summary>
    public string? Preset { get; init; }

    public bool Overwrite { get; init; }
}
