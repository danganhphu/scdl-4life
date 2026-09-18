namespace Scdl.Core.SoundCloud;

/// <summary>
/// The stable half of a <see cref="Results.ScdlError"/>.
/// </summary>
/// <remarks>
/// Separate from <see cref="SoundCloudErrors"/> on purpose. These strings are a
/// contract: callers and tests branch on them, so changing one is a breaking
/// change. The messages next door are prose and can be reworded whenever they
/// read better. Keeping them in one file also stops the codes being retyped as
/// literals at an assertion site, which is where they silently drift.
/// </remarks>
internal static class SoundCloudErrorCodes
{
    /// <summary>Advertised in <c>media.transcodings</c>, but the stream endpoint refuses to serve it.</summary>
    internal const string RungNotServed = "soundcloud.rung_not_served";

    /// <summary>The transcoding entry carried no endpoint at all.</summary>
    internal const string RungHasNoEndpoint = "soundcloud.rung_has_no_endpoint";

    /// <summary>A 200 that did not contain a URL we can fetch.</summary>
    internal const string StreamUrlUnusable = "soundcloud.stream_url_unusable";
}
