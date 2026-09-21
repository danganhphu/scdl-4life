using Scdl.Core.Downloading;
using Spectre.Console.Rendering;

namespace Scdl.Cli.Rendering;

/// <summary>How much of the transfer has happened, in whichever unit it measures.</summary>
/// <remarks>
/// Three shapes. A mux shows the position it has reached against the running
/// time, because there are no bytes to show until ffmpeg writes the trailer. A
/// download with a Content-Length renders the way Spectre's
/// <see cref="DownloadedColumn"/> would. A download without one shows what has
/// arrived and no denominator, because the built in column always renders
/// "value/total" and an unknown total came out as the placeholder MaxValue:
/// "20.0 KiB/1 byte".
/// </remarks>
internal sealed class TransferAmountColumn(TransferReadout readout) : ProgressColumn
{
    private readonly DownloadedColumn _known = new();

    protected override bool NoWrap => true;

    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
    {
        var last = readout.Last;

        if (last.IsStreamTime)
        {
            var reached = ConsoleRenderer.FormatDuration(last.StreamTime);
            var whole = ConsoleRenderer.FormatDuration(last.StreamDuration);

            return new Markup($"{reached}[grey]/[/]{whole}").RightJustified();
        }

        return task.IsIndeterminate
                   ? new Markup(ConsoleRenderer.FormatBytes((long)task.Value)).RightJustified()
                   : _known.Render(options, task, deltaTime);
    }
}
