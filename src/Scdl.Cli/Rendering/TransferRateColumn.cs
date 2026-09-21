using Scdl.Core.Downloading;
using Spectre.Console.Rendering;

namespace Scdl.Cli.Rendering;

/// <summary>Spectre's <see cref="TransferSpeedColumn"/>, silenced while a mux runs.</summary>
/// <remarks>
/// The built in column reads bytes per second off the task's value. During a mux
/// that value is seconds of audio, so the column would report a rate in bytes
/// for a number that is not bytes. Nothing is the honest render.
/// </remarks>
internal sealed class TransferRateColumn(TransferReadout readout) : ProgressColumn
{
    private readonly TransferSpeedColumn _bytes = new();

    protected override bool NoWrap => true;

    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        => readout.Last.IsStreamTime
               ? Text.Empty
               : _bytes.Render(options, task, deltaTime);
}
