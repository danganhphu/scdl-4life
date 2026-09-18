using System.Net;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.ClientId;
using Scdl.Core.SoundCloud.Models;
using Scdl.Core.Tests.Fakes;

namespace Scdl.Core.Tests;

public sealed class SoundCloudClientTests
{
    private const string ClientId = "test-client-id-0000000000000000";

    private static readonly Uri TrackUrl = new("https://soundcloud.com/artist/song");

    private static Mock<IClientIdProvider> ClientIdProvider()
    {
        var mock = new Mock<IClientIdProvider>(MockBehavior.Strict);

        // A factory, not a stored instance: a ValueTask may only be consumed
        // once, so every call has to get a fresh one.
        mock.Setup(provider => provider.GetAsync(It.IsAny<CancellationToken>()))
            .Returns(() => ValueTask.FromResult(ClientId));

        return mock;
    }

    private static SoundCloudClient CreateClient(StubHttpMessageHandler handler,
                                                 IClientIdProvider clientIds,
                                                 SoundCloudOptions? options = null)
        => new(
            handler.CreateClient(),
            clientIds,
            Options.Create(options ?? new SoundCloudOptions()),
            NullLogger<SoundCloudClient>.Instance);

    [Test]
    public async Task ResolveAsync_returns_a_single_track()
    {
        const string json = """
                            {
                              "kind": "track",
                              "id": 12345,
                              "title": "Song",
                              "user": { "username": "artist" },
                              "media": { "transcodings": [
                                { "url": "https://api-v2.soundcloud.com/media/1/x/stream/hls",
                                  "preset": "mp3_1_0",
                                  "format": { "protocol": "hls", "mime_type": "audio/mpeg" } }
                              ] }
                            }
                            """;

        var handler = StubHttpMessageHandler.ReturningJson(json);
        var client = CreateClient(handler, ClientIdProvider().Object);

        var tracks = await client.ResolveAsync(TrackUrl, CancellationToken.None);

        await Assert.That(tracks.Count).IsEqualTo(1);
        await Assert.That(tracks[0].Id).IsEqualTo(12345L);
        await Assert.That(tracks[0].DisplayArtist).IsEqualTo("artist");
    }

    [Test]
    public async Task ResolveAsync_sends_the_client_id()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"kind":"track","id":1}""");
        var client = CreateClient(handler, ClientIdProvider().Object);

        await client.ResolveAsync(TrackUrl, CancellationToken.None);

        await Assert.That(handler.Requests[0].Query.Contains(ClientId, StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task ResolveAsync_rejects_a_user_profile_with_an_actionable_message()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"kind":"user","id":1}""");
        var client = CreateClient(handler, ClientIdProvider().Object);

        var exception =
            await Assert.ThrowsAsync<ScdlException>(async ()
                => await client.ResolveAsync(TrackUrl, CancellationToken.None));

        await Assert.That(exception).IsNotNull();
        await Assert.That(exception!.Message.Contains("user profile", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// Playlists inline most tracks as id-only stubs, so the client has to make
    /// a second call and then restore playlist order, which /tracks?ids= does
    /// not preserve.
    /// </summary>
    [Test]
    public async Task ResolveAsync_hydrates_playlist_stubs_and_keeps_playlist_order()
    {
        const string playlistJson = """
                                    {
                                      "kind": "playlist",
                                      "id": 7,
                                      "title": "Set",
                                      "tracks": [ { "id": 101 }, { "id": 102 } ]
                                    }
                                    """;

        // Deliberately returned out of order to prove the client re-sorts.
        const string tracksJson = """
                                  [
                                    { "id": 102, "title": "Second",
                                      "media": { "transcodings": [ { "url": "https://x/2", "preset": "mp3_1_0",
                                        "format": { "protocol": "hls", "mime_type": "audio/mpeg" } } ] } },
                                    { "id": 101, "title": "First",
                                      "media": { "transcodings": [ { "url": "https://x/1", "preset": "mp3_1_0",
                                        "format": { "protocol": "hls", "mime_type": "audio/mpeg" } } ] } }
                                  ]
                                  """;

        var handler = StubHttpMessageHandler.Routing(
            ("/resolve", playlistJson),
            ("/tracks?ids=", tracksJson));

        var client = CreateClient(handler, ClientIdProvider().Object);

        var tracks = await client.ResolveAsync(TrackUrl, CancellationToken.None);

        await Assert.That(tracks.Count).IsEqualTo(2);
        await Assert.That(tracks[0].Id).IsEqualTo(101L);
        await Assert.That(tracks[1].Id).IsEqualTo(102L);
    }

    [Test]
    public async Task GetStreamUriAsync_passes_the_track_authorization_signature()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"url":"https://cdn.invalid/playlist.m3u8"}""");
        var client = CreateClient(handler, ClientIdProvider().Object);

        var track = new Track { Id = 1, TrackAuthorization = "sig-abc" };
        var transcoding = new Transcoding
        {
            Url = "https://api-v2.soundcloud.com/media/1/x/stream/hls", Preset = "mp3_1_0",
        };

        var result = await client.GetStreamUriAsync(track, transcoding, CancellationToken.None);

        await Assert.That(result.TryGetValue(out var uri)).IsTrue();
        await Assert.That(uri!.AbsoluteUri).IsEqualTo("https://cdn.invalid/playlist.m3u8");
        await Assert.That(handler.Requests[0].Query.Contains("track_authorization=sig-abc", StringComparison.Ordinal))
                    .IsTrue();
    }

    /// <summary>
    /// SoundCloud lists rungs it will not actually serve. This is the case that
    /// made Result worth having: a 404 here is routine, and the caller answers
    /// it by stepping down the ladder rather than by catching anything.
    /// </summary>
    [Test]
    public async Task GetStreamUriAsync_reports_an_advertised_but_unserved_rung_as_a_failure()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var client = CreateClient(handler, ClientIdProvider().Object);

        var transcoding = new Transcoding
        {
            Url = "https://api-v2.soundcloud.com/media/1/x/stream/hls", Preset = "abr_sq",
        };

        var result = await client.GetStreamUriAsync(new Track { Id = 1 }, transcoding, CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo(SoundCloudErrorCodes.RungNotServed);
        await Assert.That(result.Error.Message.Contains("abr_sq", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task GetStreamUriAsync_reports_a_transcoding_with_no_endpoint_as_a_failure()
    {
        var handler = StubHttpMessageHandler.ReturningJson("{}");
        var client = CreateClient(handler, ClientIdProvider().Object);

        var result = await client.GetStreamUriAsync(
                         new Track { Id = 1 },
                         new Transcoding { Url = null, Preset = "mp3_1_0" },
                         CancellationToken.None);

        await Assert.That(result.IsFailure).IsTrue();
        await Assert.That(result.Error.Code).IsEqualTo(SoundCloudErrorCodes.RungHasNoEndpoint);
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    /// <summary>
    /// The common case: the uploader never enabled downloads. That must be
    /// answered locally, without spending a request.
    /// </summary>
    [Test]
    public async Task TryGetOriginalUriAsync_short_circuits_when_downloads_were_never_enabled()
    {
        var handler = StubHttpMessageHandler.ReturningJson("{}");
        var client = CreateClient(handler, ClientIdProvider().Object);

        var uri = await client.TryGetOriginalUriAsync(
                      new Track { Id = 1, Downloadable = false },
                      CancellationToken.None);

        await Assert.That(uri).IsNull();
        await Assert.That(handler.Requests.Count).IsEqualTo(0);
    }

    [Test]
    public async Task TryGetOriginalUriAsync_returns_the_redirect_for_a_downloadable_track()
    {
        var handler = StubHttpMessageHandler.ReturningJson("""{"redirectUri":"https://cdn.invalid/original.wav"}""");

        var client = CreateClient(handler, ClientIdProvider().Object);

        var uri = await client.TryGetOriginalUriAsync(
                      new Track { Id = 1, Downloadable = true, HasDownloadsLeft = true },
                      CancellationToken.None);

        await Assert.That(uri!.AbsoluteUri).IsEqualTo("https://cdn.invalid/original.wav");
    }
}
