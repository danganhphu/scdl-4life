using ATL;
using static Scdl.Core.Tagging.AtlMediaTaggerLoggers;
using AtlTrack = ATL.Track;
using ScTrack = Scdl.Core.SoundCloud.Models.Track;

namespace Scdl.Core.Tagging;

/// <summary>
/// Tagging backed by ATL, which parses containers by hand in managed code. That
/// is what makes it survive trimming and Native AOT, where a reflection heavy
/// tagger would not.
/// </summary>
internal sealed class AtlMediaTagger(HttpClient http, ILogger<AtlMediaTagger> logger) : IMediaTagger
{
    public async Task<bool> TryTagAsync(string filePath,
                                        ScTrack track,
                                        SetPosition? setPosition,
                                        TagScope scope,
                                        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentNullException.ThrowIfNull(track);

        if (scope is TagScope.None)
        {
            return true;
        }

        // Only fetched for a full pass. A title-only file has nowhere to put a
        // picture, so the request is never made rather than made and discarded.
        // Artwork is fetched before the file is opened so the handle is held for
        // the shortest possible window.
        var artwork = scope is TagScope.Full
                          ? await TryFetchArtworkAsync(track, cancellationToken).ConfigureAwait(false)
                          : null;

        try
        {
            if (scope is TagScope.TitleOnly)
            {
                // ATL reads the file's existing tags into the object it returns,
                // and Save writes that whole picture back. Leaving a field unset
                // therefore keeps it rather than clearing it, so anything the
                // uploader shipped would survive untouched. Remove defaults to
                // TagType.ANY and wipes the lot, which is what makes the next
                // Save mean "the title and nothing else".
                new AtlTrack(filePath).Remove();
            }

            // No comment tag in either scope. The permalink used to go there,
            // which put a soundcloud.com link in every file the user shared.
            var tagged = new AtlTrack(filePath) { Title = track.DisplayTitle, };

            if (scope is TagScope.Full)
            {
                tagged.Artist = track.DisplayArtist;
                tagged.AlbumArtist = track.DisplayArtist;

                // A set is the only thing here that genuinely is an album, and
                // the only thing with a running order. For a single track both
                // stay unwritten rather than invented, unless SoundCloud itself
                // named an album in the publisher metadata.
                if (setPosition is { } position)
                {
                    tagged.Album = position.Album;
                    tagged.TrackNumber = position.Number;
                    tagged.TrackTotal = position.Total;
                }
                else if (track.PublisherMetadata?.AlbumTitle is { Length: > 0 } album)
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
            }

            var saved = tagged.Save();

            if (!saved)
            {
                LogTagRejected(logger, filePath);
            }

            return saved;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            LogTagFailed(logger, filePath, e.Message);

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
            return await http.GetByteArrayAsync(artworkUri, cancellationToken).ConfigureAwait(false);
        }

        // Missing cover art is cosmetic; never fail a download over it. The file
        // is already on disk by now, so anything thrown here loses a completed
        // download over a missing thumbnail.
        //
        // Deliberately not a list of types. This client carries the standard
        // resilience handler, whose total timeout surfaces as Polly's
        // TimeoutRejectedException - neither an HttpRequestException nor a
        // TimeoutException, so no narrow catch sees it. HttpClient.Timeout
        // raises a TaskCanceledException, which is indistinguishable from Ctrl+C
        // except by the token, hence the filter.
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            LogArtworkFailed(logger, e.Message);

            return null;
        }
    }
}
