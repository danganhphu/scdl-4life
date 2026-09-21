using Scdl.Core.Downloading.Hls;

namespace Scdl.Core.Tests;

public sealed class HlsPlaylistTests
{
    private static readonly Uri PlaylistUri =
        new("https://cf-hls-media.sndcdn.com/playlist/abc/stream/playlist.m3u8?token=xyz");

    [Test]
    public async Task Parse_reads_absolute_segment_uris()
    {
        const string content = """
                               #EXTM3U
                               #EXT-X-VERSION:6
                               #EXTINF:10.0,
                               https://cf-hls-media.sndcdn.com/media/0/10/a.mp3
                               #EXTINF:10.0,
                               https://cf-hls-media.sndcdn.com/media/10/20/b.mp3
                               #EXT-X-ENDLIST
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.Segments.Count).IsEqualTo(2);
        await Assert.That(playlist.Segments[0].AbsoluteUri)
                    .IsEqualTo("https://cf-hls-media.sndcdn.com/media/0/10/a.mp3");
        await Assert.That(playlist.RequiresMuxer).IsFalse();
        await Assert.That(playlist.IsEncrypted).IsFalse();
    }

    /// <summary>
    /// The running time is what a mux measures itself against. Bytes on disk say
    /// nothing there: an MP4 muxer holds every sample until it writes the
    /// trailer, so the file sits at its header length until the very end.
    /// </summary>
    [Test]
    public async Task Parse_adds_up_the_segment_durations()
    {
        const string content = """
                               #EXTM3U
                               #EXTINF:10.5,
                               a.mp3
                               #EXTINF:9.5,with a title
                               b.mp3
                               #EXT-X-ENDLIST
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.TotalDuration).IsEqualTo(TimeSpan.FromSeconds(20));
    }

    /// <summary>
    /// Zero rather than a guess. A fraction of an invented total would be the one
    /// number this tool must never show.
    /// </summary>
    [Test]
    public async Task Parse_reports_no_duration_when_the_playlist_declares_none()
    {
        const string content = """
                               #EXTM3U
                               a.mp3
                               #EXT-X-ENDLIST
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.TotalDuration).IsEqualTo(TimeSpan.Zero);
    }

    [Test]
    public async Task Parse_resolves_relative_segment_uris_against_the_playlist()
    {
        const string content = """
                               #EXTM3U
                               #EXTINF:10.0,
                               segment-0.mp3
                               #EXT-X-ENDLIST
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.Segments.Count).IsEqualTo(1);
        await Assert.That(playlist.Segments[0].AbsoluteUri)
                    .IsEqualTo("https://cf-hls-media.sndcdn.com/playlist/abc/stream/segment-0.mp3");
    }

    /// <summary>
    /// The distinction that keeps ffmpeg optional: plain segments are
    /// concatenated in managed code, fragmented MP4 is not.
    /// </summary>
    [Test]
    public async Task Parse_flags_fragmented_mp4_as_needing_a_muxer()
    {
        const string content = """
                               #EXTM3U
                               #EXT-X-MAP:URI="https://cf-hls-media.sndcdn.com/media/init.mp4"
                               #EXTINF:10.0,
                               https://cf-hls-media.sndcdn.com/media/0.m4s
                               #EXT-X-ENDLIST
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.RequiresMuxer).IsTrue();
        await Assert.That(playlist.InitializationSegment!.AbsoluteUri)
                    .IsEqualTo("https://cf-hls-media.sndcdn.com/media/init.mp4");
    }

    [Test]
    public async Task Parse_detects_encryption()
    {
        const string content = """
                               #EXTM3U
                               #EXT-X-KEY:METHOD=AES-128,URI="https://example.invalid/key"
                               #EXTINF:10.0,
                               https://cf-hls-media.sndcdn.com/media/0.mp3
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.IsEncrypted).IsTrue();
    }

    [Test]
    public async Task Parse_treats_method_none_as_unencrypted()
    {
        const string content = """
                               #EXTM3U
                               #EXT-X-KEY:METHOD=NONE
                               #EXTINF:10.0,
                               https://cf-hls-media.sndcdn.com/media/0.mp3
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.IsEncrypted).IsFalse();
    }

    [Test]
    public async Task Parse_recognises_a_master_playlist()
    {
        const string content = """
                               #EXTM3U
                               #EXT-X-STREAM-INF:BANDWIDTH=128000,CODECS="mp4a.40.2"
                               media.m3u8
                               """;

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.IsMasterPlaylist).IsTrue();
    }

    [Test]
    public async Task Parse_tolerates_windows_line_endings_and_blank_lines()
    {
        const string content = "#EXTM3U\r\n\r\n#EXTINF:10.0,\r\nhttps://host.invalid/a.mp3\r\n\r\n";

        var playlist = HlsPlaylist.Parse(content, PlaylistUri);

        await Assert.That(playlist.Segments.Count).IsEqualTo(1);
        await Assert.That(playlist.Segments[0].AbsoluteUri).IsEqualTo("https://host.invalid/a.mp3");
    }
}
