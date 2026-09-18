using Scdl.Core.Results;

namespace Scdl.Core;

/// <summary>
/// A failure whose message is already written for a human, so the CLI can print
/// it bare instead of a stack trace. Anything else escaping to the top level is
/// a bug and deserves the full trace.
/// </summary>
/// <remarks>
/// <see cref="Code"/> is the same vocabulary <see cref="ScdlError"/> uses, so a
/// failure reads the same whether it was thrown or returned. Every throw site
/// that names a known failure passes one; <see cref="ScdlErrorCode.Unspecified"/>
/// is left for guards against states that should be unreachable.
/// </remarks>
public sealed class ScdlException : Exception
{
    public ScdlException()
        => Code = ScdlErrorCode.Unspecified;

    public ScdlException(string message)
        : this(message, ScdlErrorCode.Unspecified) { }

    public ScdlException(string message, ScdlErrorCode code)
        : base(message)
        => Code = code;

    public ScdlException(string message, Exception innerException)
        : this(message, ScdlErrorCode.Unspecified, innerException) { }

    public ScdlException(string message, ScdlErrorCode code, Exception innerException)
        : base(message, innerException)
        => Code = code;

    /// <summary>What went wrong, in the form <see cref="ScdlExitCode.For"/> understands.</summary>
    public ScdlErrorCode Code { get; }
}
