using System.Globalization;
using Scdl.Core.Audio;

namespace Scdl.Core.SoundCloud.Models;

/// <summary>A single SoundCloud track as api-v2 describes it.</summary>
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
    public bool IsStub => Media is null or { Transcodings.Count: 0 };

    [JsonIgnore]
    public string DisplayArtist
        => (PublisherMetadata?.Artist, User?.Username) switch
        {
            ({ Length: > 0 } artist, _) => artist,
            (_, { Length: > 0 } username) => username,
            _ => "Unknown Artist",
        };

    /// <summary>
    /// The title as it should be shown and written, with a trailing audio
    /// extension removed - uploaders routinely leave the ".mp3" of the file they
    /// uploaded in the title itself. <see cref="Title"/> keeps the raw value.
    /// </summary>
    [JsonIgnore]
    public string DisplayTitle => Title is { Length: > 0 } title ? AudioFileExtensions.StripFrom(title) : $"track-{Id}";

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
