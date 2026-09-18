using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.Tagging;

/// <summary>Writes container metadata and cover art onto a downloaded file.</summary>
public interface IMediaTagger
{
    /// <summary>
    /// Tags a file in place. Implementations must treat tagging as best effort:
    /// the audio is already on disk, and a metadata failure is never worth
    /// discarding it over.
    /// </summary>
    /// <param name="filePath">The file to tag.</param>
    /// <param name="track">The track it holds.</param>
    /// <param name="setPosition">
    /// Where the track sat in the set it came from, or null for a single track.
    /// A single track genuinely has no album and no track number, so nothing is
    /// written for one rather than something invented.
    /// </param>
    /// <param name="cancellationToken">Cancels the artwork fetch.</param>
    Task<bool> TryTagAsync(string filePath,
                           Track track,
                           SetPosition? setPosition,
                           CancellationToken cancellationToken);
}
