namespace Scdl.Core.SoundCloud.Models;

/// <summary>How a transcoding is delivered and what container it arrives in.</summary>
public sealed record TranscodingFormat
{
    /// <summary>Either <c>progressive</c> (a single file) or <c>hls</c> (a segmented playlist).</summary>
    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }

    [JsonPropertyName("mime_type")]
    public string? MimeType { get; init; }
}
