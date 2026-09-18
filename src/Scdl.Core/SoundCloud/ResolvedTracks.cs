using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.SoundCloud;

/// <summary>What a URL turned out to be: the playable tracks, and the set they came from.</summary>
/// <remarks>
/// A bare <c>IReadOnlyList&lt;Track&gt;</c> threw the set away, and with it the
/// only chance to write an album and a track number. A set then arrived as N
/// files that no player could put back in order.
/// </remarks>
public sealed record ResolvedTracks
{
    public required IReadOnlyList<Track> Tracks { get; init; }

    /// <summary>The set's title, or null when the URL named a single track.</summary>
    public string? SetTitle { get; init; }

    /// <summary>True when the tracks came from a set rather than on their own.</summary>
    public bool IsSet => SetTitle is { Length: > 0 };

    public int Count => Tracks.Count;
}
