namespace Scdl.Core.Downloading;

/// <summary>Where the bytes of a completed download actually came from.</summary>
public enum DownloadSource
{
    /// <summary>The uploader's own file. The only genuinely lossless route.</summary>
    OriginalMaster = 0,

    /// <summary>A rung of SoundCloud's transcoding ladder.</summary>
    Transcoding = 1,
}
