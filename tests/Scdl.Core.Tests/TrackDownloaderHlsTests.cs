using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Scdl.Core.Downloading;
using Scdl.Core.Downloading.Hls;
using Scdl.Core.Results;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;
using Scdl.Core.Tests.Fakes;

namespace Scdl.Core.Tests;

/// <summary>
/// The HLS path, which is how SoundCloud serves nearly everything. It is also
/// the only part of the downloader with two ways to fail that are not failures:
/// a rung the CDN will not serve, and fragmented MP4 with no muxer installed.
/// Both must step down the ladder; everything else must stop.
/// </summary>
public sealed class TrackDownloaderHlsTests
{
    private const string PlaylistHost = "https://cf-hls-media.sndcdn.com/playlist";

    /// <summary>Two at a time, so a three segment playlist spans more than one window.</summary>
    private const int ParallelSegments = 2;

    private static string PlaylistUrlFor(string preset)
        => $"{PlaylistHost}/{preset}.m3u8";

    private static string MediaPlaylist(params string[] segments)
    {
        var builder = new StringBuilder("#EXTM3U\n#EXT-X-VERSION:3\n#EXT-X-TARGETDURATION:10\n");

        foreach (var segment in segments)
        {
            builder.Append("#EXTINF:10.0,\n").Append(segment).Append('\n');
        }

        return builder.Append("#EXT-X-ENDLIST\n").ToString();
    }

    private static Track TrackWith(params string[] presets)
        => new()
        {
            Id = 1,
            Title = "Song",
            User = new() { Username = "Artist" },
            Media = new()
            {
                Transcodings =
                [
                    .. presets.Select(preset => new Transcoding
                    {
                        Url = PlaylistUrlFor(preset),
                        Preset = preset,
                        Format = new() { Protocol = "hls", MimeType = "audio/mpeg" },
                    }),
                ],
            },
        };

    /// <summary>
    /// Serves playlists and segments by path. Anything unrouted answers 404,
    /// which is what a wrong assumption in a test should look like.
    /// </summary>
    private static StubHttpMessageHandler Serving(Dictionary<string, string> playlists,
                                                  Dictionary<string, string>? segments = null,
                                                  string? failingSegment = null)
        => new(request =>
        {
            var path = request.RequestUri?.AbsolutePath ?? string.Empty;
            var name = Path.GetFileName(path);

            if (name == failingSegment)
            {
                return new(HttpStatusCode.InternalServerError);
            }

            if (playlists.TryGetValue(name, out var playlist))
            {
                return new(HttpStatusCode.OK)
                {
                    Content = new StringContent(playlist, Encoding.UTF8, "application/x-mpegURL"),
                };
            }

            if (segments is not null && segments.TryGetValue(name, out var body))
            {
                return new(HttpStatusCode.OK) { Content = new ByteArrayContent(Encoding.UTF8.GetBytes(body)), };
            }

            return new(HttpStatusCode.NotFound);
        });

    private static Mock<ISoundCloudClient> SoundCloud()
    {
        var mock = new Mock<ISoundCloudClient>(MockBehavior.Strict);

        // The transcoding already carries the URL the stub serves, so the signed
        // location is the same URL. What matters here is the reassembly, not the
        // signing, which SoundCloudClientTests covers.
        mock.Setup(client => client.GetStreamUriAsync(
                It.IsAny<Track>(),
                It.IsAny<Transcoding>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((Track _, Transcoding transcoding, CancellationToken _)
                => Result.Success(new Uri(transcoding.Url!)));

        return mock;
    }

    private static Mock<IMediaMuxer> Muxer(bool available)
    {
        var mock = new Mock<IMediaMuxer>(MockBehavior.Strict);
        mock.SetupGet(muxer => muxer.IsAvailable).Returns(available);

        return mock;
    }

    private static TrackDownloader Downloader(StubHttpMessageHandler handler,
                                              Mock<ISoundCloudClient> soundCloud,
                                              Mock<IMediaMuxer> muxer)
        => new(
            handler.CreateClient(),
            soundCloud.Object,
            muxer.Object,
            Options.Create(new SoundCloudOptions { MaxParallelSegments = ParallelSegments }),
            NullLogger<TrackDownloader>.Instance);

    private static DownloadRequest Request(TempDirectory directory, Track track, string? preset = null)
        => new()
        {
            Track = track,

            // Always Stream: the original master path is a different test, and
            // asking for it here would mean stubbing a call that never matters.
            OutputDirectory = directory.FullPath,
            Prefer = SourcePreference.Stream,
            Preset = preset,
        };

    /// <summary>
    /// Segments are fetched in parallel windows but must be written strictly in
    /// playlist order. Concatenated audio that arrives out of order is not
    /// corrupt in any way a player reports - it just plays wrong.
    /// </summary>
    [Test]
    public async Task Segments_are_written_in_playlist_order_not_completion_order()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["mp3_1_0.m3u8"] = MediaPlaylist("a.ts", "b.ts", "c.ts"), },
            new() { ["a.ts"] = "AAAA", ["b.ts"] = "BB", ["c.ts"] = "CCCCCC", });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var result = await downloader.DownloadAsync(
                         Request(directory, TrackWith("mp3_1_0")),
                         progress: null,
                         CancellationToken.None);

        await Assert.That(await File.ReadAllTextAsync(result.FilePath)).IsEqualTo("AAAABBCCCCCC");
        await Assert.That(result.Bytes).IsEqualTo(12L);
        await Assert.That(result.Source).IsEqualTo(DownloadSource.Transcoding);
    }

    /// <summary>
    /// An HLS transfer has no Content-Length until the last segment lands, so
    /// the progress bar has to run indeterminate rather than invent a total.
    /// </summary>
    [Test]
    public async Task Hls_progress_reports_running_bytes_with_no_total()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["mp3_1_0.m3u8"] = MediaPlaylist("a.ts", "b.ts"), },
            new() { ["a.ts"] = "AAAA", ["b.ts"] = "BB", });

        var progress = new RecordingProgress();
        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        await downloader.DownloadAsync(Request(directory, TrackWith("mp3_1_0")), progress, CancellationToken.None);

        await Assert.That(progress.Ticks.Count).IsEqualTo(2);
        await Assert.That(progress.Ticks.All(tick => tick.TotalBytes is null)).IsTrue();
        await Assert.That(progress.Ticks[^1].BytesTransferred).IsEqualTo(6L);
    }

    /// <summary>
    /// Fragmented MP4 is the one case that needs ffmpeg. Missing it is not a
    /// failure: the rung below is still honest audio, so the ladder steps down.
    /// </summary>
    [Test]
    public async Task Fragmented_mp4_without_a_muxer_steps_down_to_the_next_rung()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new()
            {
                ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\nseg.m4s\n",
                ["mp3_1_0.m3u8"] = MediaPlaylist("a.ts"),
            },
            new() { ["a.ts"] = "MP3", });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var result = await downloader.DownloadAsync(
                         Request(directory, TrackWith("aac_256k", "mp3_1_0")),
                         progress: null,
                         CancellationToken.None);

        await Assert.That(result.Rung?.Preset).IsEqualTo("mp3_1_0");
        await Assert.That(Path.GetExtension(result.FilePath)).IsEqualTo(".mp3");
        await Assert.That(directory.Files.Count).IsEqualTo(1);
    }

    [Test]
    public async Task Fragmented_mp4_with_a_muxer_moves_the_muxed_file_into_place()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\nseg.m4s\n", });

        var muxer = Muxer(available: true);

        muxer.Setup(media => media.MuxAsync(
                 It.IsAny<Uri>(),
                 It.IsAny<string>(),
                 It.IsAny<string>(),
                 It.IsAny<TimeSpan>(),
                 It.IsAny<IProgress<TransferProgress>?>(),
                 It.IsAny<CancellationToken>()))
             .Returns((Uri _, string outputPath, string _, TimeSpan _, IProgress<TransferProgress>? _,
                       CancellationToken token) => File.WriteAllTextAsync(outputPath, "MUXED", token));

        var downloader = Downloader(handler, SoundCloud(), muxer);

        var result = await downloader.DownloadAsync(
                         Request(directory, TrackWith("aac_256k")),
                         progress: null,
                         CancellationToken.None);

        await Assert.That(await File.ReadAllTextAsync(result.FilePath)).IsEqualTo("MUXED");
        await Assert.That(result.Bytes).IsEqualTo(5L);

        // The muxer writes a .part file, which only counts as a download once it
        // has been moved into place under its real name.
        await Assert.That(Path.GetExtension(result.FilePath)).IsEqualTo(".m4a");
        await Assert.That(directory.Files.Count).IsEqualTo(1);
    }

    /// <summary>
    /// For a fragmented MP4 track the mux is the whole transfer - nothing else
    /// moves a byte. Dropping the sink on the way to the muxer is what left the
    /// progress bar at zero for the entire download, and the playlist's running
    /// time has to go with it or a position means nothing.
    /// </summary>
    [Test]
    public async Task Fragmented_mp4_hands_the_muxer_the_running_time_and_reports_its_position()
    {
        using var directory = new TempDirectory();

        // Two segments of ten seconds, so the total is one the parser had to add
        // up rather than read off a single line.
        var handler = Serving(
            new()
            {
                ["aac_256k.m3u8"] =
                    "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\na.m4s\n#EXTINF:10.0,\nb.m4s\n",
            });

        var muxer = Muxer(available: true);
        var seenDuration = TimeSpan.Zero;

        muxer.Setup(media => media.MuxAsync(
                 It.IsAny<Uri>(),
                 It.IsAny<string>(),
                 It.IsAny<string>(),
                 It.IsAny<TimeSpan>(),
                 It.IsAny<IProgress<TransferProgress>?>(),
                 It.IsAny<CancellationToken>()))
             .Returns((Uri _, string outputPath, string _, TimeSpan duration, IProgress<TransferProgress>? sink,
                       CancellationToken token) =>
             {
                 seenDuration = duration;

                 sink?.Report(TransferProgress.FromStreamTime(TimeSpan.FromSeconds(5), duration));
                 sink?.Report(TransferProgress.FromStreamTime(TimeSpan.FromSeconds(15), duration));

                 return File.WriteAllTextAsync(outputPath, "MUXED", token);
             });

        var progress = new RecordingProgress();
        var downloader = Downloader(handler, SoundCloud(), muxer);

        await downloader.DownloadAsync(Request(directory, TrackWith("aac_256k")), progress, CancellationToken.None);

        await Assert.That(seenDuration).IsEqualTo(TimeSpan.FromSeconds(20));
        await Assert.That(progress.Ticks.Count).IsEqualTo(2);
        await Assert.That(progress.Ticks[^1].IsStreamTime).IsTrue();
        await Assert.That(progress.Ticks[^1].Fraction).IsEqualTo(0.75d);
    }

    /// <summary>
    /// ffmpeg picks its muxer from the extension of the file it is told to
    /// write, and that file is still a .part at the time. Inferring the
    /// container from the path produced "Unable to choose an output format" and
    /// no file at all, which is why the container is passed separately.
    /// </summary>
    [Test]
    public async Task The_muxer_is_told_the_container_rather_than_left_to_read_the_temporary_name()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\nseg.m4s\n", });

        var muxer = Muxer(available: true);

        string? seenPath = null;
        string? seenContainer = null;

        muxer.Setup(media => media.MuxAsync(
                 It.IsAny<Uri>(),
                 It.IsAny<string>(),
                 It.IsAny<string>(),
                 It.IsAny<TimeSpan>(),
                 It.IsAny<IProgress<TransferProgress>?>(),
                 It.IsAny<CancellationToken>()))
             .Returns((Uri _, string outputPath, string container, TimeSpan _, IProgress<TransferProgress>? _,
                       CancellationToken token) =>
             {
                 seenPath = outputPath;
                 seenContainer = container;

                 return File.WriteAllTextAsync(outputPath, "MUXED", token);
             });

        var downloader = Downloader(handler, SoundCloud(), muxer);

        await downloader.DownloadAsync(
            Request(directory, TrackWith("aac_256k")),
            progress: null,
            CancellationToken.None);

        await Assert.That(seenPath!.EndsWith(".part", StringComparison.Ordinal)).IsTrue();
        await Assert.That(seenContainer).IsEqualTo(".m4a");
    }

    [Test]
    public async Task A_muxer_that_fails_partway_leaves_nothing_behind()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\nseg.m4s\n", });

        var muxer = Muxer(available: true);

        muxer.Setup(media => media.MuxAsync(
                 It.IsAny<Uri>(),
                 It.IsAny<string>(),
                 It.IsAny<string>(),
                 It.IsAny<TimeSpan>(),
                 It.IsAny<IProgress<TransferProgress>?>(),
                 It.IsAny<CancellationToken>()))
             .Returns((Uri _, string outputPath, string _, TimeSpan _, IProgress<TransferProgress>? _,
                       CancellationToken _) =>
             {
                 // A real muxer can fail after it has already created its output.
                 File.WriteAllText(outputPath, "half a file");

                 return Task.FromException(new ScdlException("ffmpeg failed", ScdlErrorCode.MuxerFailed));
             });

        var downloader = Downloader(handler, SoundCloud(), muxer);

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("aac_256k")),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.MuxerFailed);
        await Assert.That(directory.Files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Every_rung_needing_a_muxer_fails_with_the_install_hint()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-MAP:URI=\"init.mp4\"\n#EXTINF:10.0,\nseg.m4s\n", });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("aac_256k")),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.NoRungDownloadable);
        await Assert.That(exception.Message.Contains("ffmpeg", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// A malformed playlist says something is wrong with the request or with
    /// SoundCloud, not with this rung, so it must stop rather than quietly walk
    /// down the ladder and hand over worse audio.
    /// </summary>
    [Test]
    public async Task A_master_playlist_stops_the_run_rather_than_stepping_down()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new()
            {
                ["aac_256k.m3u8"] = "#EXTM3U\n#EXT-X-STREAM-INF:BANDWIDTH=256000\nvariant.m3u8\n",
                ["mp3_1_0.m3u8"] = MediaPlaylist("a.ts"),
            },
            new() { ["a.ts"] = "MP3", });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("aac_256k", "mp3_1_0")),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.MasterPlaylistUnexpected);
        await Assert.That(directory.Files.Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_encrypted_playlist_is_refused_rather_than_half_downloaded()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new()
            {
                ["mp3_1_0.m3u8"] =
                    "#EXTM3U\n#EXT-X-KEY:METHOD=AES-128,URI=\"key.bin\"\n#EXTINF:10.0,\na.ts\n",
            });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("mp3_1_0")),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.EncryptedPlaylist);
    }

    [Test]
    public async Task A_playlist_with_no_segments_is_an_error_not_an_empty_file()
    {
        using var directory = new TempDirectory();

        var handler = Serving(new() { ["mp3_1_0.m3u8"] = MediaPlaylist(), });

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("mp3_1_0")),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.PlaylistHasNoSegments);
        await Assert.That(directory.Files.Count).IsEqualTo(0);
    }

    /// <summary>
    /// The whole point of writing to a .part file. A truncated file left under
    /// the real name is worse than no file: it looks complete.
    /// </summary>
    [Test]
    public async Task A_segment_that_fails_midway_leaves_nothing_behind()
    {
        using var directory = new TempDirectory();

        var handler = Serving(
            new() { ["mp3_1_0.m3u8"] = MediaPlaylist("a.ts", "b.ts", "c.ts"), },
            new() { ["a.ts"] = "AAAA", ["c.ts"] = "CCCC", },
            failingSegment: "b.ts");

        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        await Assert.ThrowsAsync<HttpRequestException>(async () => await downloader.DownloadAsync(
                                                                       Request(directory, TrackWith("mp3_1_0")),
                                                                       progress: null,
                                                                       CancellationToken.None));

        await Assert.That(directory.Files.Count).IsEqualTo(0);
    }

    /// <summary>
    /// An explicit --format is never silently downgraded, so an unavailable one
    /// has to fail - and the message has to say what was on offer instead.
    /// </summary>
    [Test]
    public async Task A_preset_that_is_not_offered_names_the_ones_that_are()
    {
        using var directory = new TempDirectory();

        var handler = Serving(new());
        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    TrackWith("mp3_1_0"),
                                                                                    preset: "flac"),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.PresetNotOffered);
        await Assert.That(exception.Message.Contains("mp3_1_0", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task A_track_with_no_transcodings_has_no_playable_stream()
    {
        using var directory = new TempDirectory();

        var handler = Serving(new());
        var downloader = Downloader(handler, SoundCloud(), Muxer(available: false));

        var exception = await Assert.ThrowsAsync<ScdlException>(async () => await downloader.DownloadAsync(
                                                                                Request(
                                                                                    directory,
                                                                                    new() { Id = 1, Title = "Song", }),
                                                                                progress: null,
                                                                                CancellationToken.None));

        await Assert.That(exception!.Code).IsEqualTo(ScdlErrorCode.NoPlayableStream);
    }
}
