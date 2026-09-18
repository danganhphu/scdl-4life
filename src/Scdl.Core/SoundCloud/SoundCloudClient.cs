using System.Collections.Frozen;
using System.Globalization;
using System.Net;
using Scdl.Core.Results;
using Scdl.Core.SoundCloud.ClientId;
using Scdl.Core.SoundCloud.Models;
using static Scdl.Core.SoundCloud.SoundCloudClientLoggers;

namespace Scdl.Core.SoundCloud;

/// <summary>Thin, typed client over SoundCloud's internal api-v2.</summary>
internal sealed class SoundCloudClient(
    HttpClient http,
    IClientIdProvider clientIds,
    IOptions<SoundCloudOptions> options,
    ILogger<SoundCloudClient> logger) : ISoundCloudClient
{
    private const string ApiRoot = "https://api-v2.soundcloud.com";

    /// <summary>api-v2 refuses id batches much larger than this.</summary>
    private const int TrackBatchSize = 50;

    /// <summary>Hosts that only ever 302 somewhere else. The app's share button hands out the first one.</summary>
    private static readonly FrozenSet<string> ShortLinkHosts =
        new[] { "on.soundcloud.com", "snd.sc" }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly SoundCloudOptions _options = options.Value;

    public async Task<IReadOnlyList<Track>> ResolveAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        var canonical = await CanonicalizeAsync(url, cancellationToken).ConfigureAwait(false);
        var clientId = await clientIds.GetAsync(cancellationToken).ConfigureAwait(false);

        var requestUri = new Uri(
            $"{ApiRoot}/resolve?url={Uri.EscapeDataString(canonical.AbsoluteUri)}&client_id={clientId}");

        var json = await GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var kind = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.ResolvedKind)?.Kind;

        LogResolved(logger, canonical.AbsoluteUri, kind ?? "unknown");

        switch (kind)
        {
            case "track":
                var track = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.Track)
                            ?? throw new ScdlException("SoundCloud returned an unreadable track payload.");

                return [track];

            case "playlist":
            case "system-playlist":
                var playlist = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.Playlist)
                               ?? throw new ScdlException("SoundCloud returned an unreadable playlist payload.");

                return await HydrateAsync(playlist.Tracks, cancellationToken).ConfigureAwait(false);

            case "user":
                throw new ScdlException("That is a user profile. Point scdl at a single track or a set instead.");

            default:
                throw new ScdlException($"Unsupported SoundCloud resource (kind '{kind ?? "unknown"}').");
        }
    }

    public async Task<Result<Uri>> GetStreamUriAsync(
        Track track,
        Transcoding transcoding,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(transcoding);

        var preset = transcoding.Preset ?? "unknown";

        if (string.IsNullOrWhiteSpace(transcoding.Url))
        {
            return SoundCloudErrors.RungHasNoEndpoint(preset);
        }

        var clientId = await clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var separator = transcoding.Url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var builder = $"{transcoding.Url}{separator}client_id={clientId}";

        if (track.TrackAuthorization is { Length: > 0 } authorization)
        {
            builder += $"&track_authorization={Uri.EscapeDataString(authorization)}";
        }

        using var response = await SendAsync(new Uri(builder), cancellationToken).ConfigureAwait(false);

        // An advertised rung that answers 404 or 403 is routine, not a fault, so
        // it comes back as a failed Result rather than an exception.
        if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden)
        {
            LogRungNotServed(logger, preset, (int)response.StatusCode);

            return SoundCloudErrors.RungNotServed(preset, (int)response.StatusCode);
        }

        EnsureUsable(response);

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var location = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.StreamLocation)?.Url;

        return Uri.TryCreate(location, UriKind.Absolute, out var streamUri)
            ? streamUri
            : SoundCloudErrors.StreamUrlUnusable(preset);
    }

    public async Task<Uri?> TryGetOriginalUriAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (!track.Downloadable || !track.HasDownloadsLeft)
        {
            return null;
        }

        var clientId = await clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var trackId = track.Id.ToString(CultureInfo.InvariantCulture);
        var requestUri = new Uri($"{ApiRoot}/tracks/{trackId}/download?client_id={clientId}");

        using var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            LogOriginalUnavailable(logger, track.Id, (int)response.StatusCode);

            return null;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var redirect = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.OriginalDownload)?.RedirectUri;

        return Uri.TryCreate(redirect, UriKind.Absolute, out var originalUri) ? originalUri : null;
    }

    /// <summary>
    /// Turns whatever the user pasted into the URL api-v2 expects.
    /// </summary>
    /// <remarks>
    /// The share button in the SoundCloud app hands out an <c>on.soundcloud.com</c>
    /// link, and <c>/resolve</c> answers 404 for those rather than following the
    /// redirect, so the redirect is chased here first. The query is then dropped:
    /// it only ever carries tracking (<c>utm_*</c>, <c>si</c>) or the playlist
    /// the track was opened from (<c>in</c>), none of which /resolve wants.
    /// </remarks>
    private async Task<Uri> CanonicalizeAsync(Uri url, CancellationToken cancellationToken)
    {
        var resolved = url;

        if (ShortLinkHosts.Contains(url.Host))
        {
            using var response = await http
                                       .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                                       .ConfigureAwait(false);

            // The handler follows redirects, so the final hop is on the request
            // message that came back with the response.
            resolved = response.RequestMessage?.RequestUri ?? url;

            LogShortLinkFollowed(logger, url.AbsoluteUri, resolved.AbsoluteUri);
        }

        return resolved.Query.Length is 0
            ? resolved
            : new Uri($"{resolved.Scheme}://{resolved.Authority}{resolved.AbsolutePath}");
    }

    /// <summary>
    /// Playlist payloads inline only the first few tracks in full; the rest
    /// arrive as id-only stubs that must be fetched in batches.
    /// </summary>
    private async Task<IReadOnlyList<Track>> HydrateAsync(
        IReadOnlyList<Track> tracks,
        CancellationToken cancellationToken)
    {
        var stubIds = tracks.Where(track => track.IsStub).Select(track => track.Id).ToArray();

        if (stubIds.Length is 0)
        {
            return tracks;
        }

        LogHydrating(logger, stubIds.Length);

        var clientId = await clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var hydrated = new Dictionary<long, Track>(stubIds.Length);

        foreach (var batch in stubIds.Chunk(TrackBatchSize))
        {
            var ids = string.Join(',', batch.Select(id => id.ToString(CultureInfo.InvariantCulture)));
            var requestUri = new Uri($"{ApiRoot}/tracks?ids={ids}&client_id={clientId}");

            var json = await GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);

            foreach (var track in JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.TrackArray) ?? [])
            {
                hydrated[track.Id] = track;
            }
        }

        // Rebuild in playlist order, since /tracks?ids= does not preserve it.
        return
        [
            .. tracks
               .Select(track => hydrated.GetValueOrDefault(track.Id, track))
               .Where(track => !track.IsStub),
        ];
    }

    private async Task<string> GetStringAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);

        EnsureUsable(response);

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Turns the status codes that mean "this request will never work" into
    /// exceptions. Callers that can recover from a particular code check for it
    /// themselves before calling this.
    /// </summary>
    /// <exception cref="ScdlException">The request was rejected in a way no retry fixes.</exception>
    private void EnsureUsable(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized)
        {
            throw new ScdlException(
                _options.OAuthToken is null
                    ? "SoundCloud rejected the request (401). The track is probably private."
                    : "SoundCloud rejected the OAuth token (401). Grab a fresh one from devtools.");
        }

        if (response.StatusCode is HttpStatusCode.NotFound)
        {
            throw new ScdlException("SoundCloud returned 404. Check the URL, or the track was taken down.");
        }

        response.EnsureSuccessStatusCode();
    }

    private async Task<HttpResponseMessage> SendAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        // The request must outlive the send, so it is disposed only once the
        // response headers are in hand.
        using var request = new HttpRequestMessage(HttpMethod.Get, requestUri);

        if (_options.OAuthToken is { Length: > 0 } token)
        {
            // Deliberately not AuthenticationHeaderValue: SoundCloud expects the
            // literal "OAuth <token>" scheme that its own web player sends.
            request.Headers.TryAddWithoutValidation("Authorization", $"OAuth {token}");
        }

        return await http
                     .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                     .ConfigureAwait(false);
    }
}
