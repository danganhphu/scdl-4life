using Scdl.Core.Audio;
using Scdl.Core.SoundCloud;

namespace Scdl.Core.Downloading;

public enum SourcePreference
{
    /// <summary>Take the uploader's original master when offered, best transcoding otherwise.</summary>
    Original = 0,

    /// <summary>Always take a transcoding, even when an original is available.</summary>
    Stream = 1,
}

public enum DownloadSource
{
    /// <summary>The uploader's own file. The only genuinely lossless route.</summary>
    OriginalMaster = 0,

    /// <summary>A rung of SoundCloud's transcoding ladder.</summary>
    Transcoding = 1,
}

public sealed record DownloadRequest
{
    public required Track Track { get; init; }

    public required string OutputDirectory { get; init; }

    public SourcePreference Prefer { get; init; } = SourcePreference.Original;

    /// <summary>Forces a specific preset instead of the top of the ladder.</summary>
    public string? Preset { get; init; }

    public bool Overwrite { get; init; }
}

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

public interface ITrackDownloader
{
    Task<DownloadResult> DownloadAsync(DownloadRequest request,
                                       IProgress<TransferProgress>? progress,
                                       CancellationToken cancellationToken);
}

/// <summary>
/// Reassembles media that cannot simply be concatenated. Only fragmented MP4
/// HLS needs this; everything else is handled in managed code.
/// </summary>
public interface IMediaMuxer
{
    bool IsAvailable { get; }

    Task MuxAsync(Uri playlistUri, string outputPath, CancellationToken cancellationToken);
}
