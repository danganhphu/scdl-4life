namespace Scdl.Core.Results;

/// <summary>
/// Factories for <see cref="Result{T}"/>.
/// </summary>
/// <remarks>
/// The factories live on a non-generic type, the way <c>Task.FromResult</c> and
/// <c>Tuple.Create</c> do, so that the value type is inferred from the argument
/// instead of being written out. CA1000 forbids the alternative of hanging
/// statics off the generic type itself.
/// </remarks>
public static class Result
{
    public static Result<T> Success<T>(T value) => new(value);

    public static Result<T> Failure<T>(ScdlError error) => new(error);

    public static Result<T> Failure<T>(string code, string message) => new(new ScdlError(code, message));
}
