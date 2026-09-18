namespace Scdl.Core.SoundCloud.Models;

/// <summary>One rung as api-v2 reports it, before it is classified into an <c>AudioRung</c>.</summary>
public sealed record Transcoding
{
    /// <summary>Indirection endpoint. Resolving it yields a short lived signed CDN URL.</summary>
    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("preset")]
    public string? Preset { get; init; }

    [JsonPropertyName("quality")]
    public string? Quality { get; init; }

    [JsonPropertyName("duration")]
    public long DurationMs { get; init; }

    /// <summary>True for the 30 second preview served when the account cannot play the full track.</summary>
    [JsonPropertyName("snipped")]
    public bool Snipped { get; init; }

    [JsonPropertyName("format")]
    public TranscodingFormat? Format { get; init; }
}
