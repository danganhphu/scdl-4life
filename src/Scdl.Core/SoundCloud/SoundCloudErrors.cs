using Scdl.Core.Results;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// The failures api-v2 hands back that a caller can actually do something about.
/// Kept together because two types branch on them; anything used by one type
/// stays a literal where it is used.
/// </summary>
internal static class SoundCloudErrors
{
    /// <summary>
    /// SoundCloud listed the rung in <c>media.transcodings</c> but its stream
    /// endpoint answers 404 or 403. Observed on <c>abr_sq</c> for tracks whose
    /// <c>aac_160k</c> sibling resolves fine, so it is routine rather than
    /// exceptional: the caller should step down to the next rung.
    /// </summary>
    internal static ScdlError RungNotServed(string preset, int statusCode) =>
        new("soundcloud.rung_not_served", $"SoundCloud advertises {preset} but will not serve it (HTTP {statusCode}).");

    /// <summary>The transcoding entry carried no endpoint at all.</summary>
    internal static ScdlError RungHasNoEndpoint(string preset) =>
        new("soundcloud.rung_has_no_endpoint", $"Rung {preset} has no stream endpoint.");

    /// <summary>A 200 that did not contain a usable URL.</summary>
    internal static ScdlError StreamUrlUnusable(string preset) =>
        new("soundcloud.stream_url_unusable", $"SoundCloud returned no usable stream URL for {preset}.");
}
