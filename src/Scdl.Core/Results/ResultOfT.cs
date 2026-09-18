using System.Diagnostics.CodeAnalysis;

namespace Scdl.Core.Results;

/// <summary>
/// The outcome of an operation that either produced a <typeparamref name="T"/>
/// or did not, with a reason.
/// </summary>
/// <remarks>
/// <para>
/// This is for outcomes that are <em>expected</em>: SoundCloud advertising a rung
/// it will not serve, a preset the track does not offer. Genuine faults - a full
/// disk, a cancelled run, a bug - still throw, because a caller cannot do
/// anything sensible with them and burying them in a return value only delays
/// the stack trace.
/// </para>
/// <para>
/// A struct, so neither path allocates. <see cref="TryGetValue"/> is the intended
/// way to read it: <see cref="MaybeNullWhenAttribute"/> keeps the compiler's
/// null-flow analysis working, which is the one thing a hand-rolled Maybe type
/// throws away and cannot get back.
/// </para>
/// </remarks>
public readonly record struct Result<T>
{
    private readonly T? _value;

    internal Result(T value)
    {
        _value = value;
        Error = ScdlError.None;
        IsSuccess = true;
    }

    internal Result(ScdlError error)
    {
        _value = default;
        Error = error;
        IsSuccess = false;
    }

    /// <summary>False for <c>default(Result&lt;T&gt;)</c>, which carries <see cref="ScdlError.None"/>.</summary>
    public bool IsSuccess { get; }

    public bool IsFailure => !IsSuccess;

    /// <summary>Meaningful only when <see cref="IsFailure"/>.</summary>
    public ScdlError Error { get; }

    public static implicit operator Result<T>(T value) => new(value);

    public static implicit operator Result<T>(ScdlError error) => new(error);

    /// <summary>
    /// Reads the value. Preferred over a <c>Value</c> property because the
    /// compiler then knows <paramref name="value"/> is non-null in the true
    /// branch and refuses to let it be used in the false one.
    /// </summary>
    public bool TryGetValue([MaybeNullWhen(false)] out T value)
    {
        value = _value;

        return IsSuccess;
    }

    /// <summary>Collapses both paths to one type, for callers that must produce something either way.</summary>
    public TOut Match<TOut>(Func<T, TOut> onSuccess, Func<ScdlError, TOut> onFailure)
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return IsSuccess ? onSuccess(_value!) : onFailure(Error);
    }

    public override string ToString() => IsSuccess ? $"Success({_value})" : $"Failure({Error.Code})";
}
