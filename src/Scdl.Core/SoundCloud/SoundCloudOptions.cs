namespace Scdl.Core.SoundCloud;

/// <summary>
/// Everything the SoundCloud client needs that is not derivable from the URL
/// it was handed.
/// </summary>
/// <remarks>
/// Validated with plain predicates rather than DataAnnotations: the attribute
/// path goes through TypeConverters, which carries a RequiresUnreferencedCode
/// warning and would cost this assembly its AOT cleanliness.
/// </remarks>
public sealed class SoundCloudOptions
{
    public const string SectionName = "SoundCloud";

    internal const int MinParallelSegments = 1;
    internal const int MaxAllowedParallelSegments = 16;

    /// <summary>
    /// Go+ token, copied from the <c>Authorization</c> header of any api-v2
    /// request in browser devtools. Unlocks the 256 kbps AAC rung. Null means
    /// the free ladder, which tops out at 160 kbps.
    /// </summary>
    public string? OAuthToken { get; set; }

    /// <summary>
    /// Skips client_id scraping. Useful for tests, and for pinning a known good
    /// id if the web player markup ever changes shape.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>SoundCloud rotates the web player client_id on the order of months, not requests.</summary>
    public TimeSpan ClientIdCacheLifetime { get; set; } = TimeSpan.FromDays(7);

    /// <summary>The CDN is noticeably less cooperative with a default .NET agent string.</summary>
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/130.0.0.0 Safari/537.36";

    /// <summary>How many HLS segments to fetch at once. SoundCloud throttles aggressive clients.</summary>
    public int MaxParallelSegments { get; set; } = 4;
}
