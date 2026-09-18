namespace Scdl.Core.SoundCloud.ClientId;

/// <summary>On-disk cache entry. The id rotates on the order of months, not requests.</summary>
internal sealed record CachedClientId
{
    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("retrievedAt")]
    public required DateTimeOffset RetrievedAt { get; init; }
}
