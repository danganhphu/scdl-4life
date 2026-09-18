using Scdl.Core.Audio;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.SoundCloud;

/// <summary>Turns the transcodings on a track into a ranked list of ways to fetch it.</summary>
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
    public static bool TryFindPreset(IReadOnlyList<StreamOption> options, string preset, out StreamOption match)
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
