using System.ComponentModel.DataAnnotations;

namespace Scdl.Core.SoundCloud;

/// <summary>
/// Everything the SoundCloud client needs that is not derivable from the URL it
/// was handed.
/// </summary>
/// <remarks>
/// The DataAnnotations here are checked by <see cref="ValidateSoundCloudOptions"/>,
/// which the options source generator fills in. That matters: the generator
/// substitutes non-reflecting versions of these attributes, so the rules live
/// next to the properties they describe without costing the assembly its AOT
/// cleanliness the way a runtime <c>ValidateDataAnnotations()</c> call would.
/// </remarks>
public sealed class SoundCloudOptions
{
    public const string SectionName = "SoundCloud";

    internal const int MinParallelSegments = 1;
    internal const int MaxAllowedParallelSegments = 16;
    internal const int MinCacheDays = 1;
    internal const int MaxCacheDays = 365;

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

    /// <summary>
    /// How long a scraped client_id stays good. SoundCloud rotates it on the
    /// order of months, not requests.
    /// </summary>
    /// <remarks>
    /// Whole days rather than a <see cref="TimeSpan"/> so the value is a plain
    /// number in configuration, and so the range is expressible with the
    /// source-generated <see cref="RangeAttribute"/>. The TimeSpan overload of
    /// that attribute parses through a TypeConverter and is not trim safe.
    /// </remarks>
    [Range(MinCacheDays, MaxCacheDays)]
    public int ClientIdCacheDays { get; set; } = 7;

    /// <summary>The CDN is noticeably less cooperative with a default .NET agent string.</summary>
    [Required(AllowEmptyStrings = false)]
    public string UserAgent { get; set; } =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) " +
        "Chrome/130.0.0.0 Safari/537.36";

    /// <summary>How many HLS segments to fetch at once. SoundCloud throttles aggressive clients.</summary>
    [Range(MinParallelSegments, MaxAllowedParallelSegments)]
    public int MaxParallelSegments { get; set; } = 4;

    /// <summary><see cref="ClientIdCacheDays"/> as the type the cache actually compares against.</summary>
    public TimeSpan ClientIdCacheLifetime => TimeSpan.FromDays(ClientIdCacheDays);
}
