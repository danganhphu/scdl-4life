namespace Scdl.Core.SoundCloud.Models;

/// <summary>Every way SoundCloud is willing to serve one track's audio.</summary>
public sealed record Media
{
    [JsonPropertyName("transcodings")]
    public IReadOnlyList<Transcoding> Transcodings { get; init; } = [];
}
