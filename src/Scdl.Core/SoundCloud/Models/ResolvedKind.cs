namespace Scdl.Core.SoundCloud.Models;

/// <summary>
/// Probe shape, used to discover what <c>/resolve</c> returned before committing
/// to deserializing it as a concrete type.
/// </summary>
internal sealed record ResolvedKind
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }
}
