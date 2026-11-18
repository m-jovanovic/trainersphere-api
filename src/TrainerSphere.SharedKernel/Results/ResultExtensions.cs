namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// Railway-oriented combinators for <see cref="Result{T}"/>. Every combinator short-circuits on failure:
/// the delegate for the success track is not invoked once a result is an <see cref="Error"/>.
/// </summary>
public static partial class ResultExtensions
{
    /// <summary>
    /// Transforms the success value.
    /// </summary>
    /// <typeparam name="T">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="map">The transformation.</param>
    /// <returns>The transformed result, or the original error.</returns>
    public static Result<TOut> Map<T, TOut>(this Result<T> result, Func<T, TOut> map)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => new Success<TOut>(map(success.Value)),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    /// <summary>
    /// Continues with an operation that can fail.
    /// </summary>
    /// <typeparam name="T">The input value type.</typeparam>
    /// <typeparam name="TOut">The output value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="bind">The next operation.</param>
    /// <returns>The result of the next operation, or the original error.</returns>
    public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> bind)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => bind(success.Value),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    /// <summary>
    /// Runs a side effect with the success value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The side effect.</param>
    /// <returns>The same result.</returns>
    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
        where T : notnull
    {
        if (Unwrap(result) is Success<T> success)
        {
            action(success.Value);
        }

        return result;
    }

    /// <summary>
    /// Fails with <paramref name="error"/> when the success value does not satisfy <paramref name="predicate"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="predicate">The condition.</param>
    /// <param name="error">The error when the condition is not met.</param>
    /// <returns>The same result, or <paramref name="error"/>.</returns>
    // Pattern on Value rather than a union switch: CA1508 misreads union patterns in this shape as always true.
    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        where T : notnull =>
        Unwrap(result) is Success<T> success && !predicate(success.Value) ? error : result;

    /// <summary>
    /// Transforms the error, for example to replace a generic error with a domain-specific one.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="map">The error transformation.</param>
    /// <returns>The same success, or the transformed error.</returns>
    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map)
        where T : notnull =>
        result switch
        {
            Success<T> => result,
            Error error => map(error),
            null => throw ResultGuard.Uninitialized(),
        };

    /// <summary>
    /// Runs a side effect with the error, for example logging.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The side effect.</param>
    /// <returns>The same result.</returns>
    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
        where T : notnull
    {
        if (Unwrap(result) is Error error)
        {
            action(error);
        }

        return result;
    }

    /// <summary>
    /// Leaves the railway by handling both tracks.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="onSuccess">The success handler.</param>
    /// <param name="onFailure">The failure handler.</param>
    /// <returns>The handler output.</returns>
    public static TOut Match<T, TOut>(this Result<T> result, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        where T : notnull =>
        result switch
        {
            Success<T> success => onSuccess(success.Value),
            Error error => onFailure(error),
            null => throw ResultGuard.Uninitialized(),
        };

    /// <summary>
    /// Discards the success value, for commands whose callers only need to know that they succeeded.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>A unit result, or the original error.</returns>
    public static Result<Unit> ToUnit<T>(this Result<T> result)
        where T : notnull =>
        result.Map(static _ => default(Unit));

    private static object Unwrap<T>(Result<T> result)
        where T : notnull =>
        result.Value ?? throw ResultGuard.Uninitialized();
}
