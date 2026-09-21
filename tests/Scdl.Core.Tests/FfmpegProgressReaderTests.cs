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
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(10);

    /// <summary>
    /// One block of what ffmpeg writes. total_size is in it on purpose: it is
    /// the key that looks like the answer and is not, because an MP4 muxer holds
    /// every sample until the trailer and leaves it at the header length.
    /// </summary>
    private static string Block(string microseconds)
        => $"""
            bitrate= 160.4kbits/s
            total_size=44
            out_time_us={microseconds}
            out_time_ms={microseconds}
            out_time=00:00:01.717333
            speed=  34x
            progress=continue

            """;

    private static async Task<IReadOnlyList<TransferProgress>> ReadTicksAsync(string output,
                                                                              CancellationToken cancellationToken,
                                                                              TimeSpan? duration = null)
    {
        var progress = new RecordingProgress();

        using var reader = new StringReader(output);

        await FfmpegProgressReader.ReadAsync(reader, duration ?? Duration, progress, cancellationToken);

        return progress.Ticks;
    }

    [Test]
    public async Task Every_block_that_carries_a_position_becomes_a_tick(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("2500000") + Block("5000000"), cancellationToken);

        await Assert.That(ticks.Count).IsEqualTo(2);
        await Assert.That(ticks[0].StreamTime).IsEqualTo(TimeSpan.FromSeconds(2.5));
        await Assert.That(ticks[^1].StreamTime).IsEqualTo(TimeSpan.FromSeconds(5));
    }

    /// <summary>
    /// The position is only worth anything against the running time, and that is
    /// what makes the percentage real rather than an estimate.
    /// </summary>
    [Test]
    public async Task A_tick_measures_the_position_against_the_running_time(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("2500000"), cancellationToken);

        await Assert.That(ticks[0].IsStreamTime).IsTrue();
        await Assert.That(ticks[0].StreamDuration).IsEqualTo(Duration);
        await Assert.That(ticks[0].Fraction).IsEqualTo(0.25d);
    }

    /// <summary>ffmpeg writes N/A until the first packet reaches the output.</summary>
    [Test]
    public async Task A_position_that_is_not_a_number_is_not_a_tick(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("N/A") + Block("2500000"), cancellationToken);

        await Assert.That(ticks.Count).IsEqualTo(1);
        await Assert.That(ticks[0].StreamTime).IsEqualTo(TimeSpan.FromSeconds(2.5));
    }

    /// <summary>
    /// out_time_ms sits one line below out_time_us in every block and holds the
    /// same number, so a prefix match would report each position twice. total_size
    /// is the other trap: it is the obvious key and it does not move.
    /// </summary>
    [Test]
    [Arguments("out_time_ms=1717333")]
    [Arguments("total_size=44")]
    [Arguments("progress=end")]
    [Arguments("")]
    public async Task No_other_key_is_mistaken_for_a_position(string line, CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(line + Environment.NewLine, cancellationToken);

        await Assert.That(ticks).IsEmpty();
    }

    /// <summary>
    /// A playlist that declares no EXTINF gives nothing to measure against, and a
    /// fraction of zero would be a number invented rather than read.
    /// </summary>
    [Test]
    public async Task A_stream_of_unknown_length_reports_nothing(CancellationToken cancellationToken)
    {
        var ticks = await ReadTicksAsync(Block("2500000"), cancellationToken, TimeSpan.Zero);

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
        using var reader = new StringReader(Block("2500000"));

        await FfmpegProgressReader.ReadAsync(reader, Duration, progress: null, cancellationToken);

        await Assert.That(await reader.ReadLineAsync(cancellationToken)).IsNull();
    }
}
