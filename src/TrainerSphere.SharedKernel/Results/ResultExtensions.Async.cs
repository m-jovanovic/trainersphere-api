using System.Runtime.CompilerServices;

namespace TrainerSphere.SharedKernel.Results;

// Task and ValueTask overloads share names. An async lambda without an explicit return type converts to
// both Func<T, Task<X>> and Func<T, ValueTask<X>>, which is ambiguous (CS0121). [OverloadResolutionPriority]
// makes Task win for Result<T> and Task<Result<T>> receivers and ValueTask win for ValueTask<Result<T>>
// receivers; method groups and lambdas with a concrete return type still bind to their exact overload.

/// <content>
/// Overloads on <see cref="Result{T}"/> that take Task or ValueTask returning delegates.
/// </content>
public static partial class ResultExtensions
{
    [OverloadResolutionPriority(1)]
    public static async Task<Result<TOut>> Map<T, TOut>(this Result<T> result, Func<T, Task<TOut>> map)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => new Success<TOut>(await map(success.Value)),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    public static async ValueTask<Result<TOut>> Map<T, TOut>(this Result<T> result, Func<T, ValueTask<TOut>> map)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => new Success<TOut>(await map(success.Value)),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    [OverloadResolutionPriority(1)]
    public static async Task<Result<TOut>> Bind<T, TOut>(this Result<T> result, Func<T, Task<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => await bind(success.Value),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    public static async ValueTask<Result<TOut>> Bind<T, TOut>(this Result<T> result, Func<T, ValueTask<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull =>
        result switch
        {
            Success<T> success => await bind(success.Value),
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    [OverloadResolutionPriority(1)]
    public static async Task<Result<T>> Tap<T>(this Result<T> result, Func<T, Task> action)
        where T : notnull
    {
        if (Unwrap(result) is Success<T> success)
        {
            await action(success.Value);
        }

        return result;
    }

    public static async ValueTask<Result<T>> Tap<T>(this Result<T> result, Func<T, ValueTask> action)
        where T : notnull
    {
        if (Unwrap(result) is Success<T> success)
        {
            await action(success.Value);
        }

        return result;
    }

    [OverloadResolutionPriority(1)]
    public static async Task<Result<T>> Ensure<T>(this Result<T> result, Func<T, Task<bool>> predicate, Error error)
        where T : notnull =>
        Unwrap(result) is Success<T> success && !await predicate(success.Value) ? error : result;

    public static async ValueTask<Result<T>> Ensure<T>(this Result<T> result, Func<T, ValueTask<bool>> predicate, Error error)
        where T : notnull =>
        Unwrap(result) is Success<T> success && !await predicate(success.Value) ? error : result;

    // Without this overload an async lambda passed to TapError would bind to Action<Error> as async void.
    [OverloadResolutionPriority(1)]
    public static async Task<Result<T>> TapError<T>(this Result<T> result, Func<Error, Task> action)
        where T : notnull
    {
        if (Unwrap(result) is Error error)
        {
            await action(error);
        }

        return result;
    }
}
