using Scdl.Core.Audio;

namespace Scdl.Core.Tests;

public sealed class TranscodingCatalogTests
{
    [Test]
    [Arguments("aac_256k", 256, AudioCodec.Aac)]
    [Arguments("abr_hq", 256, AudioCodec.Aac)]
    [Arguments("aac_160k", 160, AudioCodec.Aac)]
    [Arguments("mp3_1_0", 128, AudioCodec.Mp3)]
    [Arguments("mp3_0_0", 128, AudioCodec.Mp3)]
    [Arguments("opus_0_0", 64, AudioCodec.Opus)]
    public async Task Classify_maps_known_presets_to_their_real_bitrate(string preset,
                                                                        int expectedKbps,
                                                                        AudioCodec expectedCodec)
    {
        var rung = TranscodingCatalog.Classify(preset, mimeType: null);

        await Assert.That(rung.Kbps).IsEqualTo(expectedKbps);
        await Assert.That(rung.Codec).IsEqualTo(expectedCodec);
    }

    /// <summary>
    /// The whole premise of this tool. If a 320 rung ever appears here it means
    /// someone has encoded a downloader site's marketing claim as fact.
    /// </summary>
    [Test]
    public async Task Ladder_has_no_320_kbps_rung()
    {
        var highest = TranscodingCatalog.AllRungs.Max(rung => rung.Kbps);

        await Assert.That(highest).IsEqualTo(TranscodingCatalog.LadderCeilingKbps);
        await Assert.That(highest).IsLessThan(320);
    }

    [Test]
    public async Task Only_the_256k_rungs_are_gated_behind_go_plus()
    {
        foreach (var rung in TranscodingCatalog.AllRungs)
        {
            await Assert.That(rung.RequiresGoPlus).IsEqualTo(rung.Kbps == 256);
        }
    }

    [Test]
    public async Task Classify_is_case_insensitive()
    {
        var rung = TranscodingCatalog.Classify("AAC_256K", mimeType: null);

        await Assert.That(rung.Kbps).IsEqualTo(256);
    }

    [Test]
    public async Task Classify_falls_back_to_a_prefix_match_for_a_versioned_preset()
    {
        var rung = TranscodingCatalog.Classify("mp3_1_0_v2", mimeType: null);

        await Assert.That(rung.Kbps).IsEqualTo(128);
        await Assert.That(rung.Codec).IsEqualTo(AudioCodec.Mp3);

        // The caller still sees the name SoundCloud actually sent.
        await Assert.That(rung.Preset).IsEqualTo("mp3_1_0_v2");
    }

    [Test]
    [Arguments("audio/mp4; codecs=\"mp4a.40.2\"", AudioCodec.Aac, ".m4a")]
    [Arguments("audio/mpeg", AudioCodec.Mp3, ".mp3")]
    [Arguments("audio/ogg; codecs=\"opus\"", AudioCodec.Opus, ".ogg")]
    public async Task Classify_falls_back_to_mime_type_for_an_unknown_preset(string mimeType,
                                                                             AudioCodec expectedCodec,
                                                                             string expectedExtension)
    {
        var rung = TranscodingCatalog.Classify("something_new_2027", mimeType);

        await Assert.That(rung.Codec).IsEqualTo(expectedCodec);
        await Assert.That(rung.FileExtension).IsEqualTo(expectedExtension);
        await Assert.That(rung.IsUnknown).IsTrue();
    }

    [Test]
    public async Task Classify_keeps_a_wholly_unknown_preset_rather_than_discarding_it()
    {
        var rung = TranscodingCatalog.Classify("mystery", mimeType: "application/octet-stream");

        await Assert.That(rung.Preset).IsEqualTo("mystery");
        await Assert.That(rung.Codec).IsEqualTo(AudioCodec.Unknown);
        await Assert.That(rung.IsUnknown).IsTrue();
    }

    [Test]
    [Arguments("progressive", DeliveryProtocol.Progressive)]
    [Arguments("HLS", DeliveryProtocol.Hls)]
    [Arguments("carrier-pigeon", DeliveryProtocol.Unknown)]
    public async Task ParseProtocol_reads_the_delivery_protocol(string raw, DeliveryProtocol expected)
    {
        await Assert.That(TranscodingCatalog.ParseProtocol(raw)).IsEqualTo(expected);
    }
}
