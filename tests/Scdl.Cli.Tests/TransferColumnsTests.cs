using Scdl.Cli.Rendering;
using Scdl.Core.Downloading;
using Spectre.Console;
using Spectre.Console.Testing;

namespace Scdl.Cli.Tests;

/// <summary>
/// The columns that render a transfer, over the three shapes one can take: bytes
/// against a known total, bytes against no total, and the stream time a mux
/// reports because it has no bytes to show at all. Asserted through a real
/// Progress render, because the bug they exist for was in what Spectre derives
/// from the task rather than in what a column is handed.
/// </summary>
internal sealed class TransferColumnsTests
{
    /// <summary>Wide enough that no column is wrapped out of an assertion.</summary>
    private const int ConsoleWidth = 120;

    /// <summary>What the CLI does with a tick that carries a Content-Length.</summary>
    private static readonly TransferProgress Known = TransferProgress.FromBytes(20480, 40960);

    /// <summary>What the CLI does with a tick from segment concatenation.</summary>
    private static readonly TransferProgress Unknown = TransferProgress.FromBytes(20480, null);

    /// <summary>What the CLI does with a tick from a mux.</summary>
    private static readonly TransferProgress Muxing =
        TransferProgress.FromStreamTime(TimeSpan.FromSeconds(251), TimeSpan.FromSeconds(403));

    private static async Task<string> RenderAsync(TransferProgress tick)
    {
        var console = new TestConsole().Interactive();
        console.Profile.Width = ConsoleWidth;

        var readout = new TransferReadout();

        await console.Progress()
                     .AutoClear(false)
                     .Columns(
                         new TaskDescriptionColumn(),
                         new ProgressBarColumn(),
                         new TransferPercentageColumn(),
                         new TransferAmountColumn(readout),
                         new TransferRateColumn(readout))
                     .StartAsync(context =>
                     {
                         var task = context.AddTask("Song", maxValue: 1d);

                         readout.Last = tick;
                         Apply(tick, task);

                         return Task.CompletedTask;
                     });

        return console.Output;
    }

    /// <summary>Mirrors what the download command's progress callback does with a tick.</summary>
    private static void Apply(TransferProgress tick, ProgressTask task)
    {
        if (tick.IsStreamTime)
        {
            task.IsIndeterminate = false;
            task.MaxValue = tick.StreamDuration.TotalSeconds;
            task.Value = tick.StreamTime.TotalSeconds;

            return;
        }

        if (tick.TotalBytes is > 0)
        {
            task.IsIndeterminate = false;
            task.MaxValue = tick.TotalBytes.Value;
        }
        else
        {
            task.IsIndeterminate = true;
            task.MaxValue = double.PositiveInfinity;
        }

        task.Value = tick.BytesTransferred;
    }

    /// <summary>
    /// The percentage against the placeholder MaxValue of 1 read 100 from the
    /// first tick, so every concatenated download announced itself complete while
    /// it was still running.
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

    /// <summary>
    /// A mux has a real percentage, because the position in the stream is
    /// measured against a running time the playlist stated. This is the number
    /// that used to be 0 for the whole download.
    /// </summary>
    [Test]
    public async Task A_mux_renders_its_position_in_the_stream()
    {
        var output = await RenderAsync(Muxing);

        await Assert.That(output).Contains("62%");
        await Assert.That(output).Contains("4:11/6:43");
    }

    /// <summary>
    /// Spectre's speed column reads bytes per second off the task's value, and
    /// during a mux that value is seconds of audio. A rate in bytes for a number
    /// that is not bytes is worse than no rate.
    /// </summary>
    [Test]
    public async Task A_mux_renders_no_transfer_rate()
    {
        var output = await RenderAsync(Muxing);

        await Assert.That(output).DoesNotContain("/s");
    }
}
