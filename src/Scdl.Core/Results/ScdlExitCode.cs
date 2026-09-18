namespace Scdl.Core.Results;

/// <summary>
/// The process exit codes <c>scdl</c> returns, and the mapping from
/// <see cref="ScdlErrorCode"/> onto them.
/// </summary>
/// <remarks>
/// A CLI's exit code is its machine-readable output, so a script can tell
/// "install ffmpeg" apart from "that track is gone" without parsing English.
/// The set is deliberately small: many distinct codes are harder to script
/// against than a few meaningful ones, which is why whole bands of
/// <see cref="ScdlErrorCode"/> collapse to the same value here.
/// </remarks>
public static class ScdlExitCode
{
    /// <summary>The file landed.</summary>
    public const int Success = 0;

    /// <summary>Something failed and no more specific code fits.</summary>
    public const int Failure = 1;

    /// <summary>The URL or the options were wrong. Re-running unchanged will fail the same way.</summary>
    public const int Usage = 2;

    /// <summary>The track, or anything playable on it, is not there.</summary>
    public const int Unavailable = 3;

    /// <summary>Credentials are missing or rejected. A Go+ token may be what is needed.</summary>
    public const int Unauthorized = 4;

    /// <summary>A local dependency is missing - today that means ffmpeg.</summary>
    public const int MissingDependency = 5;

    /// <summary>Interrupted, by Ctrl+C or a cancelled token. The conventional 128 + SIGINT.</summary>
    public const int Cancelled = 130;

    /// <summary>Maps a failure onto the exit code the process should return.</summary>
    public static int For(ScdlErrorCode code) => code switch
    {
        ScdlErrorCode.UnsupportedResource or
            ScdlErrorCode.PresetNotOffered => Usage,

        ScdlErrorCode.NotFound or
            ScdlErrorCode.NoPlayableStream or
            ScdlErrorCode.NoRungDownloadable or
            ScdlErrorCode.RungNotServed or
            ScdlErrorCode.RungHasNoEndpoint or
            ScdlErrorCode.StreamUrlUnusable => Unavailable,

        ScdlErrorCode.Unauthorized => Unauthorized,

        ScdlErrorCode.MuxerUnavailable => MissingDependency,

        // Everything else is a genuine fault: an unreadable payload, a muxer
        // that ran and failed, an encrypted or malformed playlist, a client_id
        // that could not be scraped. None of them tell a script anything more
        // useful than "this did not work".
        ScdlErrorCode.None or
            ScdlErrorCode.Unspecified or
            ScdlErrorCode.UnreadablePayload or
            ScdlErrorCode.UnsupportedProtocol or
            ScdlErrorCode.MuxerFailed or
            ScdlErrorCode.EncryptedPlaylist or
            ScdlErrorCode.MasterPlaylistUnexpected or
            ScdlErrorCode.PlaylistHasNoSegments or
            ScdlErrorCode.ClientIdUnavailable => Failure,

        _ => Failure,
    };
}
