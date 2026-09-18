namespace Scdl.Core.SoundCloud.Models;

/// <summary>Response of resolving a transcoding URL into a playable, signed location.</summary>
internal sealed record StreamLocation
{
    [JsonPropertyName("url")]
    public string? Url { get; init; }
}
