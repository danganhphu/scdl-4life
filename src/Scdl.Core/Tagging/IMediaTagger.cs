using Scdl.Core.SoundCloud;

namespace Scdl.Core.Tagging;

/// <summary>Writes container metadata and cover art onto a downloaded file.</summary>
public interface IMediaTagger
{
    /// <summary>
    /// Tags a file in place. Implementations must treat tagging as best effort:
    /// the audio is already on disk, and a metadata failure is never worth
    /// discarding it over.
    /// </summary>
    Task<bool> TryTagAsync(string filePath, Track track, CancellationToken cancellationToken);
}
