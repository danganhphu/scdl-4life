using Spectre.Console.Rendering;

namespace Scdl.Cli.Rendering;

/// <summary>
/// Spectre's <see cref="DownloadedColumn"/>, except that a transfer whose total
/// nobody knows shows what has arrived and no denominator.
/// </summary>
/// <remarks>
/// The built in column always renders "value/total", so an unknown total came
/// out as the placeholder MaxValue: "20.0 KiB/1 byte". Showing the one number
/// that is real is the honest form of the same line.
/// </remarks>
internal sealed class UnknownTotalDownloadedColumn : ProgressColumn
{
    private readonly DownloadedColumn _known = new();

    protected override bool NoWrap => true;

    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        => task.IsIndeterminate
               ? new Markup(ConsoleRenderer.FormatBytes((long)task.Value)).RightJustified()
               : _known.Render(options, task, deltaTime);
}
