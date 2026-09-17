using System.Globalization;

namespace Scdl.Core.SoundCloud;

/// <summary>Probe shape, used to discover what <c>/resolve</c> returned before committing to a type.</summary>
public sealed record ResolvedKind
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
}

public sealed record SoundCloudUser
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }
}

public sealed record PublisherMetadata
{
    [JsonPropertyName("artist")]
    public string? Artist { get; init; }

    [JsonPropertyName("album_title")]
    public string? AlbumTitle { get; init; }

    [JsonPropertyName("isrc")]
    public string? Isrc { get; init; }
}

public sealed record TranscodingFormat
{
    /// <summary>Either <c>progressive</c> (a single file) or <c>hls</c> (a segmented playlist).</summary>
    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
}

public sealed record Transcoding
{
    /// <summary>Indirection endpoint. Resolving it yields a short lived signed CDN URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("preset")]
    public string? Preset { get; init; }

    [JsonPropertyName("quality")]
    public string? Quality { get; init; }

    [JsonPropertyName("duration")]
    public long DurationMs { get; init; }

    /// <summary>True for the 30 second preview served when the account cannot play the full track.</summary>
    [JsonPropertyName("snipped")]
    public bool Snipped { get; init; }

    [JsonPropertyName("format")]
    public TranscodingFormat? Format { get; init; }
}

public sealed record Media
{
    [JsonPropertyName("transcodings")]
    public IReadOnlyList<Transcoding> Transcodings { get; init; } = [];
}

public sealed record Track
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("permalink_url")]
    public string? PermalinkUrl { get; init; }

    [JsonPropertyName("duration")]
    public long DurationMs { get; init; }

    [JsonPropertyName("genre")]
    public string? Genre { get; init; }

    [JsonPropertyName("artwork_url")]
    public string? ArtworkUrl { get; init; }

    /// <summary>
    /// Kept raw. SoundCloud is not consistent about the format of this across
    /// endpoints, so <see cref="ReleaseYear"/> parses it leniently instead of
    /// letting the deserializer fail the whole payload.
    /// </summary>
    [JsonPropertyName("release_date")]
    public string? ReleaseDate { get; init; }

    /// <inheritdoc cref="ReleaseDate"/>
    [JsonPropertyName("created_at")]
    public string? CreatedAt { get; init; }

    /// <summary>True when the uploader enabled downloads - the only route to the original master.</summary>
    [JsonPropertyName("downloadable")]
    public bool Downloadable { get; init; }

    [JsonPropertyName("has_downloads_left")]
    public bool HasDownloadsLeft { get; init; }

    /// <summary>Per-track signature that must accompany every stream URL request.</summary>
    [JsonPropertyName("track_authorization")]
    public string? TrackAuthorization { get; init; }

    [JsonPropertyName("user")]
    public SoundCloudUser? User { get; init; }

    [JsonPropertyName("media")]
    public Media? Media { get; init; }

    [JsonPropertyName("publisher_metadata")]
    public PublisherMetadata? PublisherMetadata { get; init; }

    /// <summary>
    /// Playlist payloads inline most of their tracks as id-only stubs, which
    /// need a second round trip through <c>/tracks?ids=</c> before they can be
    /// downloaded.
    /// </summary>
    [JsonIgnore]
    public bool IsStub => Media is null || Media.Transcodings.Count == 0;

    [JsonIgnore]
    public string DisplayArtist
        => (PublisherMetadata?.Artist, User?.Username) switch
        {
            ({ Length: > 0 } artist, _) => artist,
            (_, { Length: > 0 } username) => username,
            _ => "Unknown Artist",
        };

    [JsonIgnore]
    public string DisplayTitle => Title is { Length: > 0 } title ? title : $"track-{Id}";

    [JsonIgnore]
    public TimeSpan Duration => TimeSpan.FromMilliseconds(DurationMs);

    /// <summary>Release year, or 0 when neither date field parses.</summary>
    [JsonIgnore]
    public int ReleaseYear => TryParseYear(ReleaseDate) ?? TryParseYear(CreatedAt) ?? 0;

    /// <summary>Upgrades SoundCloud's 100x100 thumbnail URL to the 500x500 original.</summary>
    [JsonIgnore]
    public Uri? LargeArtworkUri
        => Uri.TryCreate(
               (ArtworkUrl ?? User?.AvatarUrl)?.Replace("-large.", "-t500x500.", StringComparison.Ordinal),
               UriKind.Absolute,
               out var uri)
               ? uri
               : null;

    private static int? TryParseYear(string? value)
        => DateTimeOffset.TryParse(
               value,
               CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
               out var parsed)
               ? parsed.Year
               : null;
}

public sealed record Playlist
{
    [JsonPropertyName("id")]
    public long Id { get; init; }

    [JsonPropertyName("title")]
    public string? Title { get; init; }

    [JsonPropertyName("permalink_url")]
    public string? PermalinkUrl { get; init; }

    [JsonPropertyName("user")]
    public SoundCloudUser? User { get; init; }

    [JsonPropertyName("tracks")]
    public IReadOnlyList<Track> Tracks { get; init; } = [];
}

/// <summary>Response of resolving a transcoding URL into a playable location.</summary>
public sealed record StreamLocation
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}

/// <summary>Response of <c>/tracks/{id}/download</c>, pointing at the uploader's original master.</summary>
public sealed record OriginalDownload
{
    [JsonPropertyName("redirectUri")]
    public string? RedirectUri { get; init; }
}
