namespace Scdl.Core.Results;

/// <summary>
/// Every way an operation in this tool is known to fail.
/// </summary>
/// <remarks>
/// An enum rather than strings: the set is closed, the compiler catches a typo,
/// and a switch over it can be checked for exhaustiveness. Values are grouped in
/// tens by cause, which is what <see cref="ScdlExitCode"/> maps over - keep new
/// members inside the band they belong to.
/// </remarks>
public enum ScdlErrorCode
{
    /// <summary>No error. Never meaningful on a failed result.</summary>
    None = 0,

    /// <summary>A failure with no more specific code yet.</summary>
    Unspecified = 1,

    // 10-19: what the user asked for cannot be acted on.

    /// <summary>The URL points at a user profile, or a resource kind this build does not handle.</summary>
    UnsupportedResource = 10,

    /// <summary>A 200 whose body could not be read as the shape it claimed to be.</summary>
    UnreadablePayload = 11,

    /// <summary>The preset named by <c>--format</c> is not among the ones the track offers.</summary>
    PresetNotOffered = 12,

    // 20-29: the transcoding ladder.

    /// <summary>Advertised in <c>media.transcodings</c>, but the stream endpoint refuses to serve it.</summary>
    RungNotServed = 20,

    /// <summary>The transcoding entry carried no endpoint at all.</summary>
    RungHasNoEndpoint = 21,

    /// <summary>A 200 that did not contain a URL we can fetch.</summary>
    StreamUrlUnusable = 22,

    /// <summary>The track offers no playable stream whatsoever.</summary>
    NoPlayableStream = 23,

    /// <summary>Every rung was tried and none of them produced a file.</summary>
    NoRungDownloadable = 24,

    /// <summary>SoundCloud named a delivery protocol this build does not recognise.</summary>
    UnsupportedProtocol = 25,

    // 30-39: reassembling what was downloaded.

    /// <summary>The playlist is fragmented MP4 and no muxer is installed.</summary>
    MuxerUnavailable = 30,

    /// <summary>A muxer is installed but failed to run or exited non-zero.</summary>
    MuxerFailed = 31,

    /// <summary>The playlist is encrypted, which this build does not handle.</summary>
    EncryptedPlaylist = 32,

    /// <summary>A master playlist arrived where a media playlist was expected.</summary>
    MasterPlaylistUnexpected = 33,

    /// <summary>The media playlist listed no segments.</summary>
    PlaylistHasNoSegments = 34,

    // 40-49: credentials and access.

    /// <summary>SoundCloud rejected the request, or the Go+ token, with a 401.</summary>
    Unauthorized = 40,

    /// <summary>SoundCloud answered 404 for the resource itself.</summary>
    NotFound = 41,

    // 50-59: the local environment.

    /// <summary>The client_id could not be scraped from the web player bundles.</summary>
    ClientIdUnavailable = 50,
}
