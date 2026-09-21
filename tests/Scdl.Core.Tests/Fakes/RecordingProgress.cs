using Scdl.Core.Downloading;

namespace Scdl.Core.Tests.Fakes;

/// <summary>Keeps every transfer tick in the order it was reported.</summary>
/// <remarks>
/// Hand written rather than <see cref="Progress{T}"/>, which posts through the
/// synchronization context and would leave the tail of the sequence in flight
/// when the assertions run.
/// </remarks>
internal sealed class RecordingProgress : IProgress<TransferProgress>
{
    private readonly List<TransferProgress> _ticks = [];

    public IReadOnlyList<TransferProgress> Ticks => _ticks;

    public void Report(TransferProgress value)
        => _ticks.Add(value);
}
