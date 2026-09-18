namespace Scdl.Core.SoundCloud.Models;

/// <summary>
/// Rights-holder metadata. Present on tracks distributed through a label, and
/// more trustworthy than the uploading account's display name.
/// </summary>
public sealed record PublisherMetadata
{
    [JsonPropertyName("artist")]
    public string? Artist { get; init; }

    [JsonPropertyName("album_title")]
    public string? AlbumTitle { get; init; }

    [JsonPropertyName("isrc")]
    public string? Isrc { get; init; }
}
