namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// Railway-oriented combinators for <see cref="Result{T}"/>.
/// </summary>
public static class ResultExtensions
{
    public static TOut Match<T, TOut>(this Result<T> result, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(onSuccess);
        ArgumentNullException.ThrowIfNull(onFailure);

        return result switch
        {
            Success<T> success => onSuccess(success.Value),
            Error error => onFailure(error),
        };
    }

    public static bool TryGetValue<T>(this Result<T> result, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out T value)
        where T : notnull
    {
        if (result is Success<T> success)
        {
            value = success.Value;
            return true;
        }

        value = default;
        return false;
    }

    public static bool TryGetError<T>(this Result<T> result, [System.Diagnostics.CodeAnalysis.NotNullWhen(true)] out Error? error)
        where T : notnull
    {
        error = result.Value as Error;
        return error is not null;
    }

    /// <summary>
    /// Returns the value or throws; intended for tests and for states proven unreachable.
    /// </summary>
    public static T GetValueOrThrow<T>(this Result<T> result)
        where T : notnull =>
        result switch
        {
            Success<T> success => success.Value,
            Error error => throw new InvalidOperationException($"Result is a failure: {error.Code}: {error.Message}"),
        };

    public static Error GetErrorOrThrow<T>(this Result<T> result)
        where T : notnull =>
        result switch
        {
            Success<T> => throw new InvalidOperationException("Result is a success."),
            Error error => error,
        };

    public static Result<TOut> Map<T, TOut>(this Result<T> result, Func<T, TOut> map)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return result switch
        {
            Success<T> success => Result.Success(map(success.Value)),
            Error error => Result.Failure<TOut>(error),
        };
    }

    public static Result<TOut> Bind<T, TOut>(this Result<T> result, Func<T, Result<TOut>> bind)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result switch
        {
            Success<T> success => bind(success.Value),
            Error error => Result.Failure<TOut>(error),
        };
    }

    public static Result<T> MapError<T>(this Result<T> result, Func<Error, Error> map)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return result switch
        {
            Success<T> => result,
            Error error => Result.Failure<T>(map(error)),
        };
    }

    public static Result<T> Tap<T>(this Result<T> result, Action<T> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        if (result is Success<T> success)
        {
            action(success.Value);
        }

        return result;
    }

    public static Result<T> TapError<T>(this Result<T> result, Action<Error> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        if (result.Value is Error error)
        {
            action(error);
        }

        return result;
    }

    public static Result<T> Ensure<T>(this Result<T> result, Func<T, bool> predicate, Error error)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(predicate);

        return result switch
        {
            Success<T> success => predicate(success.Value) ? result : Result.Failure<T>(error),
            Error => result,
        };
    }

    public static Result<Unit> ToUnit<T>(this Result<T> result)
        where T : notnull =>
        result.Map(static _ => default(Unit));

    public static async Task<Result<TOut>> Map<T, TOut>(this Task<Result<T>> resultTask, Func<T, TOut> map)
        where T : notnull
        where TOut : notnull =>
        (await resultTask).Map(map);

    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Result<T> result, Func<T, Task<TOut>> map)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(map);

        return result switch
        {
            Success<T> success => Result.Success(await map(success.Value)),
            Error error => Result.Failure<TOut>(error),
        };
    }

    public static async Task<Result<TOut>> MapAsync<T, TOut>(this Task<Result<T>> resultTask, Func<T, Task<TOut>> map)
        where T : notnull
        where TOut : notnull =>
        await (await resultTask).MapAsync(map);

    public static async Task<Result<TOut>> Bind<T, TOut>(this Task<Result<T>> resultTask, Func<T, Result<TOut>> bind)
        where T : notnull
        where TOut : notnull =>
        (await resultTask).Bind(bind);

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Result<T> result, Func<T, Task<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull
    {
        ArgumentNullException.ThrowIfNull(bind);

        return result switch
        {
            Success<T> success => await bind(success.Value),
            Error error => Result.Failure<TOut>(error),
        };
    }

    public static async Task<Result<TOut>> BindAsync<T, TOut>(this Task<Result<T>> resultTask, Func<T, Task<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull =>
        await (await resultTask).BindAsync(bind);

    public static async Task<Result<T>> TapAsync<T>(this Result<T> result, Func<T, Task> action)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(action);

        if (result is Success<T> success)
        {
            await action(success.Value);
        }

        return result;
    }

    public static async Task<Result<T>> TapAsync<T>(this Task<Result<T>> resultTask, Func<T, Task> action)
        where T : notnull =>
        await (await resultTask).TapAsync(action);

    public static async Task<Result<T>> Ensure<T>(this Task<Result<T>> resultTask, Func<T, bool> predicate, Error error)
        where T : notnull =>
        (await resultTask).Ensure(predicate, error);

    public static async Task<TOut> Match<T, TOut>(this Task<Result<T>> resultTask, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        where T : notnull =>
        (await resultTask).Match(onSuccess, onFailure);
}
