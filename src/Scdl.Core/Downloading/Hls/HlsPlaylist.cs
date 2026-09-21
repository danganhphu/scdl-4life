using System.Globalization;
using System.Text.RegularExpressions;

namespace Scdl.Core.Downloading.Hls;

/// <summary>
/// The parsed form of an HLS media playlist, reduced to what matters for
/// reassembling audio.
/// </summary>
public sealed partial record HlsPlaylist
{
    private const string SegmentDurationTag = "#EXTINF:";

    private HlsPlaylist() { }

    public required IReadOnlyList<Uri> Segments { get; init; }

    /// <summary>
    /// The <c>#EXT-X-MAP</c> initialisation segment. Its presence means the
    /// segments are fragmented MP4, which cannot simply be concatenated.
    /// </summary>
    public Uri? InitializationSegment { get; init; }

    /// <summary>True when <c>#EXT-X-KEY</c> declares anything other than METHOD=NONE.</summary>
    public bool IsEncrypted { get; init; }

    /// <summary>True when this is a master playlist listing variants rather than segments.</summary>
    public bool IsMasterPlaylist { get; init; }

    /// <summary>
    /// The sum of the <c>#EXTINF</c> durations, which is how long the assembled
    /// audio runs, or zero when the playlist declares none.
    /// </summary>
    /// <remarks>
    /// The only measure of a mux that moves. An MP4 muxer writes nothing until
    /// the trailer, so bytes on disk say nothing about how far it has got, while
    /// ffmpeg's reported position against this does.
    /// </remarks>
    public TimeSpan TotalDuration { get; init; }

    /// <summary>
    /// Fragmented MP4 needs a real muxer to become a playable file, so those
    /// playlists are the only ones that force an external ffmpeg.
    /// </summary>
    public bool RequiresMuxer => InitializationSegment is not null;

    [GeneratedRegex("""URI\s*=\s*"(?<uri>[^"]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex UriAttributePattern { get; }

    [GeneratedRegex("""METHOD\s*=\s*(?<method>[A-Za-z0-9-]+)""", RegexOptions.IgnoreCase)]
    private static partial Regex MethodAttributePattern { get; }

    /// <summary>
    /// Parses a playlist. Relative segment URIs are resolved against
    /// <paramref name="playlistUri"/>, which must be the URL the playlist was
    /// fetched from rather than its parent directory.
    /// </summary>
    public static HlsPlaylist Parse(string content, Uri playlistUri)
    {
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(playlistUri);

        var segments = new List<Uri>();
        Uri? initialization = null;
        var encrypted = false;
        var master = false;
        var seconds = 0d;

        foreach (var rawLine in content.Split('\n'))
        {
            var line = rawLine.Trim();

            if (line.Length is 0)
            {
                continue;
            }

            if (!line.StartsWith('#'))
            {
                if (Uri.TryCreate(playlistUri, line, out var segment))
                {
                    segments.Add(segment);
                }

                continue;
            }

            if (line.StartsWith("#EXT-X-MAP:", StringComparison.OrdinalIgnoreCase))
            {
                var value = UriAttributePattern.Match(line).Groups["uri"].Value;

                if (Uri.TryCreate(playlistUri, value, out var mapUri))
                {
                    initialization = mapUri;
                }
            }
            else if (line.StartsWith("#EXT-X-KEY:", StringComparison.OrdinalIgnoreCase))
            {
                var method = MethodAttributePattern.Match(line).Groups["method"].Value;

                encrypted = method.Length > 0 && !method.Equals("NONE", StringComparison.OrdinalIgnoreCase);
            }
            else if (line.StartsWith("#EXT-X-STREAM-INF:", StringComparison.OrdinalIgnoreCase))
            {
                master = true;
            }
            else if (line.StartsWith(SegmentDurationTag, StringComparison.OrdinalIgnoreCase))
            {
                // "#EXTINF:10.0,optional title" - the duration runs to the comma,
                // which is mandatory even when no title follows it.
                var value = line.AsSpan(SegmentDurationTag.Length);
                var comma = value.IndexOf(',');

                if (comma >= 0)
                {
                    value = value[..comma];
                }

                if (double.TryParse(value.Trim(), CultureInfo.InvariantCulture, out var duration))
                {
                    seconds += duration;
                }
            }
        }

        return new()
        {
            Segments = segments,
            InitializationSegment = initialization,
            IsEncrypted = encrypted,
            IsMasterPlaylist = master,
            TotalDuration = TimeSpan.FromSeconds(seconds),
        };
    }
}
