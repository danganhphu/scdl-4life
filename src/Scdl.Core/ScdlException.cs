namespace Scdl.Core;

/// <summary>
/// A failure whose message is already written for a human, so the CLI can print
/// it bare instead of a stack trace. Anything else escaping to the top level is
/// a bug and deserves the full trace.
/// </summary>
public sealed class ScdlException : Exception
{
    public ScdlException() { }

    public ScdlException(string message)
        : base(message) { }

    public ScdlException(string message, Exception innerException)
        : base(message, innerException) { }
}
