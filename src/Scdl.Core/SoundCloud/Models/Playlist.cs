namespace Scdl.Core.SoundCloud.Models;

/// <summary>
/// A set. Internal because callers only ever receive the flattened track list
/// that <c>ISoundCloudClient.ResolveAsync</c> returns.
/// </summary>
internal sealed record Playlist
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
