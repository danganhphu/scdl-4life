using Scdl.Core.Audio;

namespace Scdl.Core.SoundCloud;

/// <summary>One playable way to get a track, paired with what it actually costs in quality.</summary>
public readonly record struct StreamOption(Transcoding Transcoding,
                                           AudioRung Rung,
                                           DeliveryProtocol Protocol);

public static class TrackStreams
{
    /// <summary>
    /// Ranks every offered transcoding, best first. A full track always beats a
    /// snipped preview, and higher bitrate wins after that. Unrecognised presets
    /// sort last but are kept, so a newly introduced rung stays reachable.
    /// </summary>
    public static IReadOnlyList<StreamOption> Rank(Track track)
    {
        ArgumentNullException.ThrowIfNull(track);

        if (track.Media is null)
        {
            return [];
        }

        return
        [
            .. track.Media.Transcodings
                    .Where(transcoding => !string.IsNullOrWhiteSpace(transcoding.Url))
                    .Select(transcoding => new StreamOption(
                        transcoding,
                        TranscodingCatalog.Classify(transcoding.Preset, transcoding.Format?.MimeType),
                        TranscodingCatalog.ParseProtocol(transcoding.Format?.Protocol)))
                    .OrderBy(option => option.Transcoding.Snipped)
                    .ThenByDescending(option => option.Rung.Kbps)
                    .ThenBy(option => option.Rung.Preset, StringComparer.Ordinal),
        ];
    }

    /// <summary>Finds a specific preset by exact name or prefix, for <c>--format</c>.</summary>
    public static bool TryFindPreset(IReadOnlyList<StreamOption> options,
                                     string preset,
                                     out StreamOption match)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(preset);

        foreach (var option in options)
        {
            if (option.Rung.Preset.StartsWith(preset, StringComparison.OrdinalIgnoreCase))
            {
                match = option;

                return true;
            }
        }

        match = default;

        return false;
    }
}
