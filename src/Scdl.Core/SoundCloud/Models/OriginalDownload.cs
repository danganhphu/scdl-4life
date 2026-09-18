namespace Scdl.Core.SoundCloud.Models;

/// <summary>Response of <c>/tracks/{id}/download</c>, pointing at the uploader's original master.</summary>
internal sealed record OriginalDownload
{
    [JsonPropertyName("redirectUri")]
    public string? RedirectUri { get; init; }
}
