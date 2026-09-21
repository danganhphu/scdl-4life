using Spectre.Console.Rendering;

namespace Scdl.Cli.Rendering;

/// <summary>
/// Spectre's <see cref="PercentageColumn"/>, except that a transfer whose whole
/// nobody knows prints "--%" rather than a number.
/// </summary>
/// <remarks>
/// Spectre derives the percentage from Value over MaxValue and has no idea of an
/// unknown total, so a task carrying bytes against the default MaxValue of 1
/// reads 100% from its first tick and counts as finished. Segment concatenation
/// is in exactly that position, which is why this column exists rather than a
/// call to <see cref="ProgressTask.IsIndeterminate"/> alone.
/// </remarks>
internal sealed class TransferPercentageColumn : ProgressColumn
{
    private readonly PercentageColumn _known = new();

    protected override bool NoWrap => true;

    public override IRenderable Render(RenderOptions options, ProgressTask task, TimeSpan deltaTime)
        => task.IsIndeterminate
               ? new Markup("[grey]--%[/]").RightJustified()
               : _known.Render(options, task, deltaTime);
}
