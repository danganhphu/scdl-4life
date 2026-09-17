namespace Scdl.Core.SoundCloud;

public interface ISoundCloudClient
{
    /// <summary>Expands a track, set or playlist URL into a flat list of playable tracks.</summary>
    Task<IReadOnlyList<Track>> ResolveAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Turns a transcoding entry into the signed CDN location that actually
    /// carries audio. The signature is short lived, so call this immediately
    /// before transferring rather than up front.
    /// </summary>
    Task<Uri> GetStreamUriAsync(Track track, Transcoding transcoding, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the uploader's original master. Returns null when downloads were
    /// never enabled or the quota is exhausted, which is the common case.
    /// </summary>
    Task<Uri?> TryGetOriginalUriAsync(Track track, CancellationToken cancellationToken);
}
