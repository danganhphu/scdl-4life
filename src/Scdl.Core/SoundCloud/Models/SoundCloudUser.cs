namespace Scdl.Core.SoundCloud.Models;

/// <summary>The account that uploaded a track.</summary>
public sealed record SoundCloudUser
{
    [JsonPropertyName("username")]
    public string? Username { get; init; }

    [JsonPropertyName("permalink")]
    public string? Permalink { get; init; }

    [JsonPropertyName("avatar_url")]
    public string? AvatarUrl { get; init; }
}
