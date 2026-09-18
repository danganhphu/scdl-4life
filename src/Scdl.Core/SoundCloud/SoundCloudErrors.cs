using Scdl.Core.Results;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// The failures api-v2 hands back that a caller can actually do something about.
/// </summary>
/// <remarks>
/// The codes live on <see cref="ScdlErrorCode"/>; this is only where each one
/// gets its sentence. Splitting them that way means the code is a compiler
/// checked symbol while the message stays prose that can be reworded freely.
/// </remarks>
internal static class SoundCloudErrors
{
    /// <summary>
    /// SoundCloud listed the rung in <c>media.transcodings</c> but its stream
    /// endpoint answers 404 or 403. Observed on <c>abr_sq</c> for tracks whose
    /// <c>aac_160k</c> sibling resolves fine, so it is routine rather than
    /// exceptional: the caller should step down to the next rung.
    /// </summary>
    internal static ScdlError RungNotServed(string preset, int statusCode)
        => new(
            ScdlErrorCode.RungNotServed,
            $"SoundCloud advertises {preset} but will not serve it (HTTP {statusCode}).");

    /// <summary>The transcoding entry carried no endpoint at all.</summary>
    internal static ScdlError RungHasNoEndpoint(string preset)
        => new(ScdlErrorCode.RungHasNoEndpoint, $"Rung {preset} has no stream endpoint.");

    /// <summary>A 200 that did not contain a usable URL.</summary>
    internal static ScdlError StreamUrlUnusable(string preset)
        => new(
            ScdlErrorCode.StreamUrlUnusable,
            $"SoundCloud returned no usable stream URL for {preset}.");
}
