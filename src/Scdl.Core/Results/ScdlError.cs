namespace Scdl.Core.Results;

/// <summary>
/// Why an operation did not produce a value. A struct, so carrying one costs
/// nothing on the failure path - which matters, because the point of returning
/// errors instead of throwing them is that failure is expected here.
/// </summary>
/// <remarks>
/// Named ScdlError rather than Error because <c>Error</c> is a reserved word in
/// Visual Basic, which CA1716 flags on any publicly visible type.
/// </remarks>
/// <param name="Code">
/// Stable and machine-readable. Callers branch on this; the message is for
/// humans and may be reworded at any time.
/// </param>
/// <param name="Message">One sentence, already phrased for the person running the tool.</param>
public readonly record struct ScdlError(string Code, string Message)
{
    /// <summary>The absence of an error. Never inspect this on a successful result.</summary>
    public static readonly ScdlError None = new(string.Empty, string.Empty);

    public override string ToString() => Message;
}
