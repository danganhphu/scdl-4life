using Scdl.Cli.Rendering;
using Scdl.Core.Audio;
using Scdl.Core.Downloading;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;
using Spectre.Console.Testing;

namespace Scdl.Cli.Tests;

/// <summary>
/// Asserts on the text a user would see, through Spectre's own TestConsole,
/// rather than on calls made to a mock console. A renderer that writes the wrong
/// thing is the only way this class can be wrong.
/// </summary>
public sealed class ConsoleRendererTests
{
    /// <summary>Wide enough that a table never wraps and breaks an assertion on its content.</summary>
    private const int ConsoleWidth = 200;

    private static TestConsole Console()
    {
        var console = new TestConsole();
        console.Profile.Width = ConsoleWidth;

        return console;
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
                        Url = $"https://api-v2.soundcloud.com/media/1/{preset}/stream/hls",
                        Preset = preset,
                        Format = new() { Protocol = "hls", MimeType = "audio/mpeg" },
                    }),
                ],
            },
        };

    [Test]
    [Arguments(0L, "0.0 B")]
    [Arguments(512L, "512.0 B")]
    [Arguments(1024L, "1.0 KiB")]
    [Arguments(1536L, "1.5 KiB")]
    [Arguments(1048576L, "1.0 MiB")]
    [Arguments(1073741824L, "1.0 GiB")]
    public async Task FormatBytes_scales_to_binary_units(long bytes, string expected)
    {
        await Assert.That(ConsoleRenderer.FormatBytes(bytes)).IsEqualTo(expected);
    }

    /// <summary>Terabytes are beyond the largest unit, so the scale has to stop rather than run off the array.</summary>
    [Test]
    public async Task FormatBytes_stops_at_the_largest_unit_it_knows()
    {
        await Assert.That(ConsoleRenderer.FormatBytes(5L * 1024 * 1024 * 1024 * 1024)).IsEqualTo("5120.0 GiB");
    }

    /// <summary>
    /// Track titles routinely contain square brackets - "[Free DL]", "[Extended
    /// Mix]" - which Spectre reads as markup. Unescaped, they either vanish or
    /// throw. This is the reason every interpolated value goes through Escape.
    /// </summary>
    [Test]
    public async Task Square_brackets_in_a_message_survive_rather_than_being_read_as_markup()
    {
        var console = Console();

        new ConsoleRenderer(console).Error("Could not fetch [Free DL] Some Track");

        await Assert.That(console.Output.Contains("[Free DL]", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task A_bracketed_track_title_renders_in_the_heading()
    {
        var console = Console();
        var track = TrackWith("mp3_1_0") with { Title = "Some Track [Extended Mix]" };

        new ConsoleRenderer(console).TrackHeading(track, 1, 1);

        await Assert.That(console.Output.Contains("[Extended Mix]", StringComparison.Ordinal)).IsTrue();
        await Assert.That(console.Output.Contains("Artist", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>A position counter on a single track is noise; on a set it is the only way to follow along.</summary>
    [Test]
    public async Task The_heading_counts_positions_only_for_a_set()
    {
        var single = Console();
        var set = Console();
        var track = TrackWith("mp3_1_0");

        new ConsoleRenderer(single).TrackHeading(track, 1, 1);
        new ConsoleRenderer(set).TrackHeading(track, 1, 3);

        await Assert.That(single.Output.Contains("(1/1)", StringComparison.Ordinal)).IsFalse();
        await Assert.That(set.Output.Contains("(1/3)", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task The_ladder_suggests_oauth_when_the_ceiling_is_out_of_reach()
    {
        var console = Console();
        var track = TrackWith("mp3_1_0", "opus_0_0");

        new ConsoleRenderer(console).LadderTable(track, track.RankStreams(), hasToken: false);

        await Assert.That(console.Output.Contains("--oauth", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// Observed against a live track on a free account: the hint vanished the
    /// moment a token was passed, so the run that most needs an explanation got
    /// none. Passing a token is not the same as the account having Go+.
    /// </summary>
    [Test]
    public async Task The_ladder_says_the_ceiling_did_not_unlock_when_a_token_was_passed()
    {
        var console = Console();
        var track = TrackWith("mp3_1_0", "opus_0_0");

        new ConsoleRenderer(console).LadderTable(track, track.RankStreams(), hasToken: true);

        await Assert.That(console.Output.Contains("did not unlock", StringComparison.Ordinal)).IsTrue();

        // Suggesting the flag to somebody who just used it is noise.
        await Assert.That(console.Output.Contains("--oauth", StringComparison.Ordinal)).IsFalse();
    }

    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task The_ladder_stays_quiet_when_the_ceiling_is_already_offered(bool hasToken)
    {
        var console = Console();
        var track = TrackWith("aac_256k", "mp3_1_0");

        new ConsoleRenderer(console).LadderTable(track, track.RankStreams(), hasToken);

        await Assert.That(console.Output.Contains("--oauth", StringComparison.Ordinal)).IsFalse();
        await Assert.That(console.Output.Contains("did not unlock", StringComparison.Ordinal)).IsFalse();
    }

    /// <summary>The claim this whole tool exists to make. It belongs in the output, not just the README.</summary>
    [Test]
    public async Task The_ladder_says_there_is_no_320_rung()
    {
        var console = Console();
        var track = TrackWith("mp3_1_0");

        new ConsoleRenderer(console).LadderTable(track, track.RankStreams(), hasToken: true);

        await Assert.That(console.Output.Contains("no 320 kbps rung", StringComparison.Ordinal)).IsTrue();
    }

    [Test]
    public async Task Saved_names_the_original_master_rather_than_a_preset()
    {
        var console = Console();

        new ConsoleRenderer(console).Saved(
            new() { FilePath = @"C:\music\Artist - Song.wav", Source = DownloadSource.OriginalMaster, Bytes = 1024, });

        await Assert.That(console.Output.Contains("original master", StringComparison.Ordinal)).IsTrue();
        await Assert.That(console.Output.Contains("1.0 KiB", StringComparison.Ordinal)).IsTrue();
    }

    /// <summary>
    /// Codec names are acronyms, so the enum's "Mp3" spelling is wrong wherever
    /// it is shown to a person.
    /// </summary>
    [Test]
    public async Task Saved_spells_a_codec_as_an_acronym()
    {
        var console = Console();

        new ConsoleRenderer(console).Saved(
            new()
            {
                FilePath = @"C:\music\Artist - Song.mp3",
                Source = DownloadSource.Transcoding,
                Bytes = 4096,
                Rung = TranscodingCatalog.Classify("mp3_1_0", mimeType: null),
            });

        await Assert.That(console.Output.Contains("MP3", StringComparison.Ordinal)).IsTrue();
        await Assert.That(console.Output.Contains("Mp3", StringComparison.Ordinal)).IsFalse();
    }
}
