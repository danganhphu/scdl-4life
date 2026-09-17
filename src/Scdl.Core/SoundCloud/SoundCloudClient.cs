using System.Globalization;
using System.Net;

namespace Scdl.Core.SoundCloud;

/// <summary>Thin, typed client over SoundCloud's internal api-v2.</summary>
public sealed partial class SoundCloudClient : ISoundCloudClient
{
    /// <summary>api-v2 refuses id batches much larger than this.</summary>
    private const int TrackBatchSize = 50;

    private const string ApiRoot = "https://api-v2.soundcloud.com";

    private readonly HttpClient _http;
    private readonly IClientIdProvider _clientIds;
    private readonly SoundCloudOptions _options;
    private readonly ILogger<SoundCloudClient> _logger;

    public SoundCloudClient(HttpClient http,
                            IClientIdProvider clientIds,
                            IOptions<SoundCloudOptions> options,
                            ILogger<SoundCloudClient> logger)
    {
        ArgumentNullException.ThrowIfNull(options);

        _http = http;
        _clientIds = clientIds;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<Track>> ResolveAsync(Uri url, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(url);

        var clientId = await _clientIds.GetAsync(cancellationToken).ConfigureAwait(false);

        var requestUri = new Uri($"{ApiRoot}/resolve?url={Uri.EscapeDataString(url.AbsoluteUri)}&client_id={clientId}");

        var json = await GetStringAsync(requestUri, cancellationToken).ConfigureAwait(false);
        var kind = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.ResolvedKind)?.Kind;

        LogResolved(url.AbsoluteUri, kind ?? "unknown");

        switch (kind)
        {
            case "track":
                var track = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.Track) ??
                            throw new ScdlException("SoundCloud returned an unreadable track payload.");

                return [track];

            case "playlist":
            case "system-playlist":
                var playlist = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.Playlist) ??
                               throw new ScdlException("SoundCloud returned an unreadable playlist payload.");

                return await HydrateAsync(playlist.Tracks, cancellationToken).ConfigureAwait(false);

            case "user":
                throw new ScdlException("That is a user profile. Point scdl at a single track or a set instead.");

            default:
                throw new ScdlException($"Unsupported SoundCloud resource (kind '{kind ?? "unknown"}').");
        }
    }

    /// <summary>
    /// Playlist payloads inline only the first few tracks in full; the rest
    /// arrive as id-only stubs that must be fetched in batches.
    /// </summary>
    private async Task<IReadOnlyList<Track>> HydrateAsync(IReadOnlyList<Track> tracks,
                                                          CancellationToken cancellationToken)
    {
        var stubIds = tracks.Where(track => track.IsStub).Select(track => track.Id).ToArray();

        if (stubIds.Length == 0)
        {
            return tracks;
        }

        LogHydrating(stubIds.Length);

        var clientId = await _clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var hydrated = new Dictionary<long, Track>();

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

    public async Task<Uri> GetStreamUriAsync(Track track,
                                             Transcoding transcoding,
                                             CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);
        ArgumentNullException.ThrowIfNull(transcoding);

        if (string.IsNullOrWhiteSpace(transcoding.Url))
        {
            throw new ScdlException("That rung has no stream endpoint.");
        }

        var clientId = await _clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var separator = transcoding.Url.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        var builder = $"{transcoding.Url}{separator}client_id={clientId}";

        if (track.TrackAuthorization is { Length: > 0 } authorization)
        {
            builder += $"&track_authorization={Uri.EscapeDataString(authorization)}";
        }

        var json = await GetStringAsync(new Uri(builder), cancellationToken).ConfigureAwait(false);
        var location = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.StreamLocation)?.Url;

        if (!Uri.TryCreate(location, UriKind.Absolute, out var streamUri))
        {
            throw new ScdlException("SoundCloud did not return a usable stream URL for that rung.");
        }

        return streamUri;
    }

    public async Task<Uri?> TryGetOriginalUriAsync(Track track, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (!track.Downloadable || !track.HasDownloadsLeft)
        {
            return null;
        }

        var clientId = await _clientIds.GetAsync(cancellationToken).ConfigureAwait(false);
        var requestUri = new Uri(
            $"{ApiRoot}/tracks/{track.Id.ToString(CultureInfo.InvariantCulture)}/download?client_id={clientId}");

        using var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.NotFound)
        {
            LogOriginalUnavailable(track.Id, (int)response.StatusCode);

            return null;
        }

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var redirect = JsonSerializer.Deserialize(json, SoundCloudJsonContext.Default.OriginalDownload)?.RedirectUri;

        return Uri.TryCreate(redirect, UriKind.Absolute, out var originalUri) ? originalUri : null;
    }

    private async Task<string> GetStringAsync(Uri requestUri, CancellationToken cancellationToken)
    {
        using var response = await SendAsync(requestUri, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.Unauthorized)
        {
            throw new ScdlException(
                _options.OAuthToken is null
                    ? "SoundCloud rejected the request (401). The track is probably private."
                    : "SoundCloud rejected the OAuth token (401). Grab a fresh one from devtools.");
        }

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            throw new ScdlException("SoundCloud returned 404. Check the URL, or the track was taken down.");
        }

        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
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

        return await _http
                     .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                     .ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved {Url} to kind '{Kind}'.")]
    private partial void LogResolved(string url, string kind);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Hydrating {Count} playlist track stub(s).")]
    private partial void LogHydrating(int count);

    [LoggerMessage(Level = LogLevel.Debug, Message = "No original master for track {TrackId} (HTTP {StatusCode}).")]
    private partial void LogOriginalUnavailable(long trackId, int statusCode);
}
