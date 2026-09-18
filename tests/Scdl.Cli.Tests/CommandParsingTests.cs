using System.CommandLine;

namespace Scdl.Cli.Tests;

/// <summary>
/// The command surface itself: which options exist, what they accept, and where
/// they are allowed to appear. Parsing never invokes an action, so none of this
/// touches the network.
/// </summary>
public sealed class CommandParsingTests
{
    private const string TrackUrl = "https://soundcloud.com/artist/track";

    private static ParseResult Parse(params string[] args)
        => CommandFactory.CreateRoot().Parse(args);

    [Test]
    [Arguments("get")]
    [Arguments("formats")]
    public async Task The_root_offers_the_command(string name)
    {
        var result = Parse(name, TrackUrl);

        await Assert.That(result.Errors.Count).IsEqualTo(0);
        await Assert.That(result.CommandResult.Command.Name).IsEqualTo(name);
    }

    /// <summary>
    /// <c>--prefer</c> is a closed set, so a typo has to fail at parse time. Left
    /// open, "origional" would silently mean "stream" and quietly hand over a
    /// transcoding when the lossless master was there for the taking.
    /// </summary>
    [Test]
    [Arguments("original")]
    [Arguments("stream")]
    public async Task Prefer_accepts_its_two_values(string value)
    {
        var result = Parse("get", TrackUrl, "--prefer", value);

        await Assert.That(result.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task Prefer_rejects_anything_else()
    {
        var result = Parse("get", TrackUrl, "--prefer", "lossless");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// Both are declared Recursive on the root, which is what lets them be typed
    /// after the subcommand - where anyone would type them.
    /// </summary>
    [Test]
    [Arguments("--oauth", "token-value")]
    [Arguments("--verbose", null)]
    public async Task A_root_option_is_accepted_after_the_subcommand(string option, string? value)
    {
        string[] args = value is null ? ["get", TrackUrl, option] : ["get", TrackUrl, option, value];

        var result = Parse(args);

        await Assert.That(result.Errors.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("-v")]
    [Arguments("-o")]
    [Arguments("-f")]
    public async Task The_short_aliases_parse(string alias)
    {
        string[] args = alias is "-v" ? ["get", TrackUrl, alias] : ["get", TrackUrl, alias, "value"];

        var result = Parse(args);

        await Assert.That(result.Errors.Count).IsEqualTo(0);
    }

    [Test]
    public async Task An_unknown_option_is_an_error_rather_than_being_ignored()
    {
        var result = Parse("get", TrackUrl, "--bitrate", "320");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// <c>formats</c> only reads. Giving it the download options would imply it
    /// writes a file, so they are deliberately not on it.
    /// </summary>
    [Test]
    [Arguments("--out")]
    [Arguments("--overwrite")]
    [Arguments("--no-tags")]
    public async Task Formats_does_not_take_the_download_options(string option)
    {
        var result = Parse("formats", TrackUrl, option, "value");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }

    /// <summary>
    /// The help text is the first thing a new user reads, and the 320 kbps claim
    /// is the one they arrive believing.
    /// </summary>
    [Test]
    public async Task The_root_description_corrects_the_320_myth()
    {
        var description = CommandFactory.CreateRoot().Description ?? string.Empty;

        await Assert.That(description.Contains("no 320 kbps rung", StringComparison.Ordinal)).IsTrue();
    }
}
