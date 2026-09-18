using Scdl.Core.Audio;
using Scdl.Core.Downloading;
using Scdl.Core.SoundCloud;
using Scdl.Core.SoundCloud.Models;

namespace Scdl.Cli.Rendering;

/// <summary>
/// All user facing output. Kept to Spectre's simple renderables on purpose:
/// prompts and the type conversion helpers behind them are the parts of the
/// library that do not survive Native AOT.
/// </summary>
internal sealed class ConsoleRenderer(IAnsiConsole console)
{
    private static readonly string[] Units = ["B", "KiB", "MiB", "GiB"];

    public void TrackHeading(Track track, int index, int total)
    {
        var position = total > 1 ? $"[grey]({index}/{total})[/] " : string.Empty;

        console.MarkupLine($"{position}[bold]{Escape(track.DisplayArtist)}[/] - {Escape(track.DisplayTitle)}");
    }

    public void LadderTable(Track track, IReadOnlyList<StreamOption> options, bool hasGoPlusToken)
    {
        var table = new Table()
                    .Border(TableBorder.Rounded)
                    .AddColumn("Preset")
                    .AddColumn(new TableColumn("Bitrate").RightAligned())
                    .AddColumn("Codec")
                    .AddColumn("Delivery")
                    .AddColumn("Note");

        foreach (var option in options)
        {
            var bitrate = option.Rung.IsUnknown ? "[grey]?[/]" : $"{option.Rung.Kbps} kbps";

            var note = option switch
            {
                { Transcoding.Snipped: true } => "[red]preview only[/]",
                { Rung.RequiresGoPlus: true } => "[green]Go+ unlocked[/]",
                _ => string.Empty,
            };

            table.AddRow(
                Escape(option.Rung.Preset),
                bitrate,
                Describe(option.Rung.Codec),
                option.Protocol.ToString().ToLowerInvariant(),
                note);
        }

        table.AddRow(
            "[italic]original[/]",
            track is { Downloadable: true, HasDownloadsLeft: true } ? "[green]lossless[/]" : "[grey]-[/]",
            string.Empty,
            string.Empty,
            track is { Downloadable: true, HasDownloadsLeft: true }
                ? "uploader enabled downloads"
                : "[grey]uploader did not enable downloads[/]");

        console.Write(table);

        if (!hasGoPlusToken && !options.Any(option => option.Rung.Kbps >= TranscodingCatalog.LadderCeilingKbps))
        {
            console.MarkupLine(
                $"[yellow]![/] {TranscodingCatalog.LadderCeilingKbps} kbps AAC is Go+ only. " +
                "Pass [bold]--oauth[/] to check whether it unlocks here.");
        }

        console.MarkupLine(
            $"[grey]SoundCloud stores no 320 kbps rung; {TranscodingCatalog.LadderCeilingKbps} kbps AAC is the ceiling.[/]");
    }

    public void Saved(DownloadResult result)
    {
        var source = result switch
        {
            { Source: DownloadSource.OriginalMaster } => "[green]original master[/]",
            { Rung: { } rung } => $"{Escape(rung.Preset)} ({rung.Kbps} kbps {Describe(rung.Codec)})",
            _ => "transcoding",
        };

        console.MarkupLine($"  source  {source}");
        console.MarkupLine($"  saved   [bold]{Escape(result.FilePath)}[/] [grey]({FormatBytes(result.Bytes)})[/]");
    }

    public void Warning(string message)
        => console.MarkupLine($"[yellow]![/] {Escape(message)}");

    public void Error(string message)
        => console.MarkupLine($"[red]x[/] {Escape(message)}");

    public void Note(string message)
        => console.MarkupLine($"[grey]{Escape(message)}[/]");

    /// <summary>
    /// Track titles routinely contain square brackets, which Spectre reads as
    /// markup. Everything interpolated from the API goes through here.
    /// </summary>
    private static string Escape(string value)
        => Markup.Escape(value);

    /// <summary>
    /// Codec names are acronyms, so the enum's PascalCase spelling ("Mp3") is
    /// wrong everywhere it is shown. One place decides how they read.
    /// </summary>
    private static string Describe(AudioCodec codec)
        => codec.ToString().ToUpperInvariant();

    public static string FormatBytes(long bytes)
    {
        double value = bytes;
        var unit = 0;

        while (value >= 1024 && unit < Units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:F1} {Units[unit]}";
    }
}
