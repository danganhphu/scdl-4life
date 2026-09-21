using Scdl.Cli.Rendering;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scdl.Cli.Tests;

/// <summary>
/// The two progress columns that know a total can be unknown. Asserted through a
/// real Progress render rather than by calling Render directly, because the bug
/// they exist for was in what Spectre derives from the task, not in what the
/// column is handed.
/// </summary>
internal sealed class TransferColumnsTests
{
    /// <summary>Wide enough that no column is wrapped out of an assertion.</summary>
    private const int ConsoleWidth = 120;

    /// <summary>What the CLI does with a tick that carries a Content-Length.</summary>
    private static void Known(ProgressTask task)
    {
        task.MaxValue = 40960;
        task.IsIndeterminate = false;
        task.Value = 20480;
    }

    /// <summary>What the CLI does with a tick from HLS or from a mux.</summary>
    private static void Unknown(ProgressTask task)
    {
        task.MaxValue = double.PositiveInfinity;
        task.IsIndeterminate = true;
        task.Value = 20480;
    }

    private static async Task<string> RenderAsync(Action<ProgressTask> arrange)
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = ConsoleWidth;

        await console.Progress()
                     .AutoClear(false)
                     .Columns(
                         new TaskDescriptionColumn(),
                         new ProgressBarColumn(),
                         new UnknownTotalPercentageColumn(),
                         new UnknownTotalDownloadedColumn(),
                         new TransferSpeedColumn())
                     .StartAsync(context =>
                     {
                         arrange(context.AddTask("Song", maxValue: 1d));

                         return Task.CompletedTask;
                     });

        return console.Output;
    }

    /// <summary>
    /// The percentage against the placeholder MaxValue of 1 read 100 from the
    /// first tick, so every HLS download announced itself complete while it was
    /// still running.
    /// </summary>
    [Test]
    public async Task An_unknown_total_shows_no_percentage()
    {
        var output = await RenderAsync(Unknown);

        await Assert.That(output).Contains("--%");
        await Assert.That(output).DoesNotContain("100%");
    }

    /// <summary>The bytes that have arrived are real and worth showing; the denominator is not.</summary>
    [Test]
    public async Task An_unknown_total_shows_the_bytes_so_far_and_no_denominator()
    {
        var output = await RenderAsync(Unknown);

        await Assert.That(output).Contains("20.0 KiB");

        // Not a bare "/" - the speed column two places along renders one.
        await Assert.That(output).DoesNotContain("20.0/");
    }

    /// <summary>A transfer that does know its size must still render the way Spectre would.</summary>
    [Test]
    public async Task A_known_total_renders_a_percentage_and_a_denominator()
    {
        var output = await RenderAsync(Known);

        await Assert.That(output).Contains("50%");
        await Assert.That(output).Contains("20.0/40.0 KiB");
    }
}
