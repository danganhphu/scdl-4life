using System.CommandLine;

namespace Scdl.Cli.Tests;

/// <summary>
/// The hand-written URL parser exists because the published binary once rejected
/// every URL: <c>Argument&lt;Uri&gt;</c> resolves its converter through
/// TypeDescriptor and the trimmer removes it. Parsing is the one part of the CLI
/// that can be exercised without touching the network, and it is also the part
/// that has already been wrong once.
/// </summary>
public sealed class UrlArgumentTests
{
    private static ParseResult Parse(params string[] args)
        => CommandFactory.CreateRoot().Parse(args);

    private static bool AnyErrorContains(ParseResult result, string text)
        => result.Errors.Any(error => error.Message.Contains(text, StringComparison.Ordinal));

    [Test]
    [Arguments("https://soundcloud.com/artist/track")]
    [Arguments("http://soundcloud.com/artist/track")]
    [Arguments("https://on.soundcloud.com/jlol120XDIDzXD464B")]
    public async Task A_valid_url_parses_without_error(string url)
    {
        var result = Parse("get", url);

        await Assert.That(result.Errors.Count).IsEqualTo(0);
    }

    [Test]
    [Arguments("soundcloud.com/artist/track")]
    [Arguments("/artist/track")]
    [Arguments("not a url at all")]
    public async Task A_relative_url_is_rejected_as_not_absolute(string url)
    {
        var result = Parse("get", url);

        await Assert.That(AnyErrorContains(result, "absolute")).IsTrue();
    }

    /// <summary>
    /// An absolute URL with the wrong scheme parses as a Uri perfectly well, so
    /// nothing but this check stops <c>file:///</c> reaching HttpClient.
    /// </summary>
    [Test]
    [Arguments("ftp://soundcloud.com/artist/track")]
    [Arguments("file:///C:/secrets.txt")]
    public async Task A_non_http_scheme_is_rejected(string url)
    {
        var result = Parse("get", url);

        await Assert.That(AnyErrorContains(result, "http or https")).IsTrue();
    }

    /// <summary>The message names the input, so a typo is obvious without re-reading the shell history.</summary>
    [Test]
    public async Task The_error_quotes_the_input_it_rejected()
    {
        var result = Parse("get", "htps://soundcloud.com/artist/track");

        await Assert.That(AnyErrorContains(result, "htps://")).IsTrue();
    }

    [Test]
    public async Task Both_commands_parse_a_url_the_same_way()
    {
        var formats = Parse("formats", "soundcloud.com/artist/track");

        await Assert.That(AnyErrorContains(formats, "absolute")).IsTrue();
    }

    [Test]
    public async Task A_missing_url_is_an_error_rather_than_a_null_download()
    {
        var result = Parse("get");

        await Assert.That(result.Errors.Count).IsGreaterThan(0);
    }
}
