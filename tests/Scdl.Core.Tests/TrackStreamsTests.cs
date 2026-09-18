using Scdl.Core.Audio;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Core.Tests;

public sealed class TrackStreamsTests
{
    private static Transcoding Rung(string preset, string protocol = "hls", bool snipped = false) => new()
    {
        Url = $"https://api-v2.soundcloud.com/media/soundcloud:tracks:1/{preset}/stream/{protocol}",
        Preset = preset,
        Snipped = snipped,
        Format = new TranscodingFormat { Protocol = protocol, MimeType = "audio/mpeg" },
    };

    private static Track TrackWith(params Transcoding[] transcodings) => new()
    {
        Id = 1,
        Title = "Song",
        Media = new Media { Transcodings = transcodings },
    };

    [Test]
    public async Task Rank_puts_the_highest_bitrate_first()
    {
        var track = TrackWith(Rung("opus_0_0"), Rung("mp3_1_0"), Rung("aac_256k"));

        var ranked = TrackStreams.Rank(track);

        await Assert.That(ranked.Count).IsEqualTo(3);
        await Assert.That(ranked[0].Rung.Preset).IsEqualTo("aac_256k");
        await Assert.That(ranked[1].Rung.Preset).IsEqualTo("mp3_1_0");
        await Assert.That(ranked[2].Rung.Preset).IsEqualTo("opus_0_0");
    }

    /// <summary>
    /// A full 128 kbps track is more useful than a 256 kbps 30 second preview,
    /// so snipped rungs sort below everything playable regardless of bitrate.
    /// </summary>
    [Test]
    public async Task Rank_sorts_a_snipped_preview_below_a_full_lower_bitrate_rung()
    {
        var track = TrackWith(Rung("aac_256k", snipped: true), Rung("mp3_1_0"));

        var ranked = TrackStreams.Rank(track);

        await Assert.That(ranked[0].Rung.Preset).IsEqualTo("mp3_1_0");
        await Assert.That(ranked[1].Transcoding.Snipped).IsTrue();
    }

    [Test]
    public async Task Rank_drops_transcodings_with_no_endpoint()
    {
        var track = TrackWith(Rung("mp3_1_0"), new Transcoding { Preset = "aac_256k", Url = null });

        var ranked = TrackStreams.Rank(track);

        await Assert.That(ranked.Count).IsEqualTo(1);
        await Assert.That(ranked[0].Rung.Preset).IsEqualTo("mp3_1_0");
    }

    [Test]
    public async Task Rank_returns_empty_when_there_is_no_media_block()
    {
        var ranked = TrackStreams.Rank(new Track { Id = 1 });

        await Assert.That(ranked.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Rank_reads_the_delivery_protocol()
    {
        var track = TrackWith(Rung("mp3_0_0", protocol: "progressive"));

        var ranked = TrackStreams.Rank(track);

        await Assert.That(ranked[0].Protocol).IsEqualTo(DeliveryProtocol.Progressive);
    }

    [Test]
    public async Task TryFindPreset_matches_on_a_prefix()
    {
        var ranked = TrackStreams.Rank(TrackWith(Rung("aac_256k"), Rung("mp3_1_0")));

        var found = TrackStreams.TryFindPreset(ranked, "aac", out var match);

        await Assert.That(found).IsTrue();
        await Assert.That(match.Rung.Kbps).IsEqualTo(256);
    }

    [Test]
    public async Task TryFindPreset_reports_a_miss_rather_than_throwing()
    {
        var ranked = TrackStreams.Rank(TrackWith(Rung("mp3_1_0")));

        var found = TrackStreams.TryFindPreset(ranked, "flac", out _);

        await Assert.That(found).IsFalse();
    }
}
