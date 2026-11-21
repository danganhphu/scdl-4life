namespace Scdl.Core.Tagging;

/// <summary>How much metadata a downloaded file should end up carrying.</summary>
public enum TagScope
{
    /// <summary>
    /// Write nothing. Whatever the container arrived with is left exactly as it
    /// arrived, which is not the same as the file being clean.
    /// </summary>
    None = 0,

    /// <summary>
    /// The title, and nothing else. Every other tag is cleared first, cover art
    /// included. This is the default: these files get shared, and an artist,
    /// an album or a picture travels with them.
    /// </summary>
    TitleOnly = 1,

    /// <summary>Everything SoundCloud says about the track, cover art included.</summary>
    Full = 2,
}
