using Scdl.Core.Downloading;
using Scdl.Core.Downloading.Hls;
using Scdl.Core.Tests.Fakes;

namespace Scdl.Core.Tests;

/// <summary>
/// The parser over ffmpeg's -progress stream. It is a type of its own precisely
/// so that these run with no ffmpeg on PATH, which is the state of this machine
/// and is not guaranteed on either CI runner.
/// </summary>
internal sealed class FfmpegProgressReaderTests
{
    /// <summary>One block of what ffmpeg writes, minus the keys nobody reads.</summary>
    private static string Block(string size)
        => $"""
            bitrate= 160.4kbits/s
            total_size={size}
            out_time_us=1717333
            out_time=00:00:01.717333
            speed=  34x
            progress=continue

            """;

    private static async Task<IReadOnlyList<TransferProgress>> ReadTicksAsync(string output,
                                                                              CancellationToken cancellationToken)
    {
        var progress = new RecordingProgress();

        using var reader = new StringReader(output);

        await FfmpegProgressReader.ReadAsync(reader, progress, cancellationToken);

        return progress.Ticks;
    }

    [Test]
    public async Task Every_block_that_carries_a_size_becomes_a_tick(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("20480") + Block("61440"), cancellationToken);

        await Assert.That(ticks.Count).IsEqualTo(2);
        await Assert.That(ticks[0].BytesTransferred).IsEqualTo(20480L);
        await Assert.That(ticks[^1].BytesTransferred).IsEqualTo(61440L);
    }

    /// <summary>
    /// A mux has no total until ffmpeg exits, so a tick that invented one would
    /// put the bar at a percentage nobody can stand behind.
    /// </summary>
    [Test]
    public async Task A_tick_from_a_mux_carries_no_total(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("20480"), cancellationToken);

        await Assert.That(ticks[0].TotalBytes).IsNull();
    }

    /// <summary>ffmpeg writes N/A until the first packet reaches the output.</summary>
    [Test]
    public async Task A_size_that_is_not_a_number_is_not_a_tick(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("N/A") + Block("20480"), cancellationToken);

        await Assert.That(ticks.Count).IsEqualTo(1);
        await Assert.That(ticks[0].BytesTransferred).IsEqualTo(20480L);
    }

    /// <summary>
    /// total_size_estimate and the rest share the shape of the line that matters,
    /// and a prefix match would read the wrong number off any of them.
    /// </summary>
    [Test]
    [Arguments("total_size_estimate=9999")]
    [Arguments("out_time_us=1717333")]
    [Arguments("progress=end")]
    [Arguments("")]
    public async Task No_other_key_is_mistaken_for_a_size(string line, CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(line + Environment.NewLine, cancellationToken);

        await Assert.That(ticks).IsEmpty();
    }

    /// <summary>
    /// The pipe has to be drained whether or not anyone is listening. Letting it
    /// fill blocks ffmpeg itself, which turns a missing progress bar into a
    /// download that never finishes.
    /// </summary>
    [Test]
    public async Task The_stream_is_drained_with_no_sink_attached(CancellationToken cancellationToken)
    {
        using var reader = new StringReader(Block("20480"));

        await FfmpegProgressReader.ReadAsync(reader, progress: null, cancellationToken);

        await Assert.That(await reader.ReadLineAsync(cancellationToken)).IsNull();
    }
}
