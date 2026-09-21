using Scdl.Core.Downloading;

namespace Scdl.Cli.Rendering;

/// <summary>The last tick a transfer reported, shared with the columns that render it.</summary>
/// <remarks>
/// Spectre hands a column nothing but the task, and a task carries one number
/// with no unit attached. A mux counts stream time where a download counts
/// bytes, so the columns have to be told which one they are looking at. One
/// transfer runs at a time, so one holder covers it.
/// </remarks>
internal sealed class TransferReadout
{
    public TransferProgress Last { get; set; }
}
