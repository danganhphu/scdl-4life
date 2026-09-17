using ATL;
using Scdl.Core.SoundCloud;
using AtlTrack = ATL.Track;
using ScTrack = Scdl.Core.SoundCloud.Track;

namespace Scdl.Core.Tagging;

/// <summary>
/// Tagging backed by ATL, which parses containers by hand in managed code. That
/// is what makes it survive trimming and Native AOT, where a reflection heavy
/// tagger would not.
/// </summary>
public sealed partial class AtlMediaTagger(HttpClient http, ILogger<AtlMediaTagger> logger) : IMediaTagger
{
    private readonly HttpClient _http = http;
    private readonly ILogger<AtlMediaTagger> _logger = logger;

    public async Task<bool> TryTagAsync(string filePath, ScTrack track, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(track);

        // Artwork is fetched before the file is opened so the handle is held for
        // the shortest possible window.
        var artwork = await TryFetchArtworkAsync(track, cancellationToken).ConfigureAwait(false);

        try
        {
            var tagged = new AtlTrack(filePath)
            {
                Title = track.DisplayTitle,
                Artist = track.DisplayArtist,
                AlbumArtist = track.DisplayArtist,
                Comment = track.PermalinkUrl ?? string.Empty,
            };

            if (track.PublisherMetadata?.AlbumTitle is { Length: > 0 } album)
            {
                tagged.Album = album;
            }

            if (track.Genre is { Length: > 0 } genre)
            {
                tagged.Genre = genre;
            }

            if (track.ReleaseYear > 0)
            {
                tagged.Year = track.ReleaseYear;
            }

            if (artwork is not null)
            {
                tagged.EmbeddedPictures.Add(PictureInfo.fromBinaryData(artwork, PictureInfo.PIC_TYPE.Front));
            }

            var saved = tagged.Save();

            if (!saved)
            {
                LogTagRejected(filePath);
            }

            return saved;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogTagFailed(filePath, e.Message);

            return false;
        }
    }

    private async Task<byte[]?> TryFetchArtworkAsync(ScTrack track, CancellationToken cancellationToken)
    {
        if (track.LargeArtworkUri is not { } artworkUri)
        {
            return null;
        }

        try
        {
            return await _http.GetByteArrayAsync(artworkUri, cancellationToken).ConfigureAwait(false);
        }
        catch (HttpRequestException e)
        {
            // Missing cover art is cosmetic; never fail a download over it.
            LogArtworkFailed(e.Message);

            return null;
        }
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Tagger refused to save {FilePath}.")]
    private partial void LogTagRejected(string filePath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Could not tag {FilePath}: {Reason}")]
    private partial void LogTagFailed(string filePath, string reason);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Could not fetch cover art: {Reason}")]
    private partial void LogArtworkFailed(string reason);
}
