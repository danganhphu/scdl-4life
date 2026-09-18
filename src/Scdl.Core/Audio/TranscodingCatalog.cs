using System.Collections.Frozen;

namespace Scdl.Core.Audio;

/// <summary>
/// Maps SoundCloud preset identifiers onto the bitrate the CDN genuinely serves.
/// </summary>
/// <remarks>
/// There is no 320 kbps rung on this ladder and there never has been. Sites
/// advertising a "320 kbps SoundCloud download" fetch the 128 kbps rung and
/// re-encode it upwards, which costs roughly 2.5x the bytes and adds no
/// information; lossy re-encoding only compounds. The same applies to their
/// "WAV" option, which wraps 128 kbps audio in a lossless container.
/// <para>
/// Above 128 kbps there are exactly two honest routes: <c>aac_256k</c>, which
/// needs a Go+ OAuth token, and the uploader's original master, which needs the
/// uploader to have enabled downloads.
/// </para>
/// </remarks>
public static class TranscodingCatalog
{
    private static readonly FrozenDictionary<string, AudioRung> ByPreset = new AudioRung[]
    {
        // begin-snippet: transcoding-ladder
        new("aac_256k", 256, AudioCodec.Aac, ".m4a", RequiresGoPlus: true),
        new("abr_hq", 256, AudioCodec.Aac, ".m4a", RequiresGoPlus: true),
        new("aac_160k", 160, AudioCodec.Aac, ".m4a", RequiresGoPlus: false),
        new("abr_sq", 160, AudioCodec.Aac, ".m4a", RequiresGoPlus: false),
        new("mp3_1_0", 128, AudioCodec.Mp3, ".mp3", RequiresGoPlus: false),
        new("mp3_0_1", 128, AudioCodec.Mp3, ".mp3", RequiresGoPlus: false),
        new("mp3_0_0", 128, AudioCodec.Mp3, ".mp3", RequiresGoPlus: false),
        new("mp3_standard", 128, AudioCodec.Mp3, ".mp3", RequiresGoPlus: false),
        new("aac_96k", 96, AudioCodec.Aac, ".m4a", RequiresGoPlus: false),
        new("opus_0_0", 64, AudioCodec.Opus, ".ogg", RequiresGoPlus: false),

        // end-snippet
    }.ToFrozenDictionary(rung => rung.Preset, StringComparer.OrdinalIgnoreCase);

    /// <summary>The highest bitrate reachable without a Go+ token, in kbps.</summary>
    public const int FreeTierCeilingKbps = 160;

    /// <summary>The highest bitrate SoundCloud serves at all, in kbps.</summary>
    public const int LadderCeilingKbps = 256;

    /// <summary>Every rung, best first. Useful for help text and tests.</summary>
    public static IReadOnlyList<AudioRung> AllRungs { get; } =
        [.. ByPreset.Values.OrderByDescending(rung => rung.Kbps).ThenBy(rung => rung.Preset, StringComparer.Ordinal)];

    /// <summary>
    /// Resolves a preset to its rung. Presets are versioned, so an exact miss
    /// falls back to a prefix match and then to the MIME type. An unrecognised
    /// preset is returned with a zero bitrate rather than discarded, so a newly
    /// introduced rung stays downloadable instead of disappearing.
    /// </summary>
    public static AudioRung Classify(string? preset, string? mimeType)
    {
        var name = preset ?? string.Empty;

        if (ByPreset.TryGetValue(name, out var exact))
        {
            return exact;
        }

        foreach (var candidate in ByPreset.Values)
        {
            if (name.StartsWith(candidate.Preset, StringComparison.OrdinalIgnoreCase))
            {
                return candidate with { Preset = name };
            }
        }

        return ClassifyByMimeType(name, mimeType);
    }

    private static AudioRung ClassifyByMimeType(string preset, string? mimeType)
    {
        var mime = mimeType ?? string.Empty;

        if (mime.Contains("mp4", StringComparison.OrdinalIgnoreCase))
        {
            return new(preset, 0, AudioCodec.Aac, ".m4a", RequiresGoPlus: false);
        }

        if (mime.Contains("mpeg", StringComparison.OrdinalIgnoreCase))
        {
            return new(preset, 0, AudioCodec.Mp3, ".mp3", RequiresGoPlus: false);
        }

        if (mime.Contains("ogg", StringComparison.OrdinalIgnoreCase) ||
            mime.Contains("opus", StringComparison.OrdinalIgnoreCase))
        {
            return new(preset, 0, AudioCodec.Opus, ".ogg", RequiresGoPlus: false);
        }

        return new(preset, 0, AudioCodec.Unknown, ".bin", RequiresGoPlus: false);
    }

    public static DeliveryProtocol ParseProtocol(string? protocol)
        => protocol switch
        {
            not null when protocol.Equals("progressive", StringComparison.OrdinalIgnoreCase) =>
                DeliveryProtocol.Progressive,
            not null when protocol.Equals("hls", StringComparison.OrdinalIgnoreCase) =>
                DeliveryProtocol.Hls,
            _ => DeliveryProtocol.Unknown,
        };
}
