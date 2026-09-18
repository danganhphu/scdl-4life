using Scdl.Core.Results;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.SoundCloud;

public interface ISoundCloudClient
{
    /// <summary>Expands a track, set or playlist URL into the playable tracks behind it.</summary>
    Task<ResolvedTracks> ResolveAsync(Uri url, CancellationToken cancellationToken);

    /// <summary>
    /// Turns a transcoding entry into the signed CDN location that actually
    /// carries audio. The signature is short lived, so call this immediately
    /// before transferring rather than up front.
    /// </summary>
    /// <returns>
    /// A failure when SoundCloud advertises the rung but will not serve it,
    /// which is routine rather than exceptional - the caller steps down to the
    /// next rung. A genuinely broken request still throws.
    /// </returns>
    Task<Result<Uri>> GetStreamUriAsync(Track track, Transcoding transcoding, CancellationToken cancellationToken);

    /// <summary>
    /// Resolves the uploader's original master. Returns null when downloads were
    /// never enabled or the quota is exhausted, which is the common case.
    /// </summary>
    Task<Uri?> TryGetOriginalUriAsync(Track track, CancellationToken cancellationToken);
}
