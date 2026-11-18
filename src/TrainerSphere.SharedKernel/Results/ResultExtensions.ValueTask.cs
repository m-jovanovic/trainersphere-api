using System.Runtime.CompilerServices;

namespace TrainerSphere.SharedKernel.Results;

/// <content>
/// Overloads on <see cref="ValueTask{TResult}"/> of <see cref="Result{T}"/>. A chain stays on ValueTask until a
/// step returns a Task, after which it continues as Task.
/// </content>
public static partial class ResultExtensions
{
    public static async ValueTask<Result<TOut>> Map<T, TOut>(this ValueTask<Result<T>> source, Func<T, TOut> map)
        where T : notnull
        where TOut : notnull =>
        (await source).Map(map);

    public static async Task<Result<TOut>> Map<T, TOut>(this ValueTask<Result<T>> source, Func<T, Task<TOut>> map)
        where T : notnull
        where TOut : notnull =>
        await (await source).Map(map);

    [OverloadResolutionPriority(1)]
    public static async ValueTask<Result<TOut>> Map<T, TOut>(this ValueTask<Result<T>> source, Func<T, ValueTask<TOut>> map)
        where T : notnull
        where TOut : notnull =>
        await (await source).Map(map);

    public static async ValueTask<Result<TOut>> Bind<T, TOut>(this ValueTask<Result<T>> source, Func<T, Result<TOut>> bind)
        where T : notnull
        where TOut : notnull =>
        (await source).Bind(bind);

    public static async Task<Result<TOut>> Bind<T, TOut>(this ValueTask<Result<T>> source, Func<T, Task<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull =>
        await (await source).Bind(bind);

    [OverloadResolutionPriority(1)]
    public static async ValueTask<Result<TOut>> Bind<T, TOut>(this ValueTask<Result<T>> source, Func<T, ValueTask<Result<TOut>>> bind)
        where T : notnull
        where TOut : notnull =>
        await (await source).Bind(bind);

    public static async ValueTask<Result<T>> Tap<T>(this ValueTask<Result<T>> source, Action<T> action)
        where T : notnull =>
        (await source).Tap(action);

    public static async Task<Result<T>> Tap<T>(this ValueTask<Result<T>> source, Func<T, Task> action)
        where T : notnull =>
        await (await source).Tap(action);

    [OverloadResolutionPriority(1)]
    public static async ValueTask<Result<T>> Tap<T>(this ValueTask<Result<T>> source, Func<T, ValueTask> action)
        where T : notnull =>
        await (await source).Tap(action);

    public static async ValueTask<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, bool> predicate, Error error)
        where T : notnull =>
        (await source).Ensure(predicate, error);

    public static async Task<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, Task<bool>> predicate, Error error)
        where T : notnull =>
        await (await source).Ensure(predicate, error);

    [OverloadResolutionPriority(1)]
    public static async ValueTask<Result<T>> Ensure<T>(this ValueTask<Result<T>> source, Func<T, ValueTask<bool>> predicate, Error error)
        where T : notnull =>
        await (await source).Ensure(predicate, error);

    public static async ValueTask<Result<T>> MapError<T>(this ValueTask<Result<T>> source, Func<Error, Error> map)
        where T : notnull =>
        (await source).MapError(map);

    public static async ValueTask<Result<T>> TapError<T>(this ValueTask<Result<T>> source, Action<Error> action)
        where T : notnull =>
        (await source).TapError(action);

    [OverloadResolutionPriority(1)]
    public static async Task<Result<T>> TapError<T>(this ValueTask<Result<T>> source, Func<Error, Task> action)
        where T : notnull =>
        await (await source).TapError(action);

    public static async ValueTask<TOut> Match<T, TOut>(this ValueTask<Result<T>> source, Func<T, TOut> onSuccess, Func<Error, TOut> onFailure)
        where T : notnull =>
        (await source).Match(onSuccess, onFailure);
}
