namespace Scdl.Core.SoundCloud;

/// <summary>
/// Supplies the api-v2 <c>client_id</c>. SoundCloud never publishes it, so it
/// has to come out of the web player bundles.
/// </summary>
public interface IClientIdProvider
{
    ValueTask<string> GetAsync(CancellationToken cancellationToken);
}

/// <summary>On-disk cache entry. The id rotates on the order of months, not requests.</summary>
public sealed record CachedClientId
{
    [JsonPropertyName("clientId")]
    public required string ClientId { get; init; }

    [JsonPropertyName("retrievedAt")]
    public required DateTimeOffset RetrievedAt { get; init; }
}
