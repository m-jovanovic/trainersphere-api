namespace Domain.Results;

/// <summary>
/// Contains extension methods for working with the <see cref="Result"/> class.
/// </summary>
public static class ResultExtensions
{
    /// <summary>
    /// Maps the success result based on the specified mapping function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="func">The mapping function.</param>
    /// <returns>The mapped result.</returns>
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> result, Func<TIn, TOut> func) =>
        result.IsSuccess ? func(result.Value) : Result.Failure<TOut>(result.Error);

    /// <summary>
    /// Maps the success result based on the specified mapping function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="resultTask">The result task.</param>
    /// <param name="func">The mapping function.</param>
    /// <returns>The mapped result.</returns>
    public static async Task<Result<TOut>> Map<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, TOut> func) =>
        (await resultTask).Map(func);

    /// <summary>
    /// Binds the success result based on the specified binding function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="func">The binding function.</param>
    /// <returns>The bound result.</returns>
    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Result<TOut>> func) =>
        result.IsSuccess ? func(result.Value) : Result.Failure<TOut>(result.Error);

    /// <summary>
    /// Binds the success result based on the specified binding function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="func">The binding function.</param>
    /// <returns>The bound result.</returns>
    public static async Task<Result<TOut>> Bind<TIn, TOut>(this Result<TIn> result, Func<TIn, Task<Result<TOut>>> func) =>
        result.IsSuccess ? await func(result.Value) : Result.Failure<TOut>(result.Error);

    /// <summary>
    /// Binds the success result based on the specified binding function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="resultTask">The result task.</param>
    /// <param name="func">The binding function.</param>
    /// <returns>The bound result.</returns>
    public static async Task<Result<TOut>> Bind<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Result<TOut>> func) =>
        (await resultTask).Bind(func);

    /// <summary>
    /// Binds the success result based on the specified binding function, otherwise returns a failure result.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <typeparam name="TOut">The output type.</typeparam>
    /// <param name="resultTask">The result task.</param>
    /// <param name="func">The binding function.</param>
    /// <returns>The bound result.</returns>
    public static async Task<Result<TOut>> Bind<TIn, TOut>(this Task<Result<TIn>> resultTask, Func<TIn, Task<Result<TOut>>> func) =>
        await (await resultTask).Bind(func);

    /// <summary>
    /// Tap will execute the provided action if the result is successful.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The action.</param>
    /// <returns>The same result.</returns>
    public static Result<TIn> Tap<TIn>(this Result<TIn> result, Action action)
    {
        if (result.IsSuccess)
        {
            action();
        }

        return result;
    }

    /// <summary>
    /// Tap will execute the provided action if the result is successful.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="action">The action.</param>
    /// <returns>The same result.</returns>
    public static Result<TIn> Tap<TIn>(this Result<TIn> result, Action<TIn> action)
    {
        if (result.IsSuccess)
        {
            action(result.Value);
        }

        return result;
    }

    /// <summary>
    /// Tap will execute the provided action if the result is successful.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <param name="resultTask">The result task.</param>
    /// <param name="action">The action.</param>
    /// <returns>The same result.</returns>
    public static async Task<Result<TIn>> Tap<TIn>(this Task<Result<TIn>> resultTask, Action action) => (await resultTask).Tap(action);

    /// <summary>
    /// Tap will execute the provided action if the result is successful.
    /// </summary>
    /// <typeparam name="TIn">The input type.</typeparam>
    /// <param name="resultTask">The result task.</param>
    /// <param name="action">The action.</param>
    /// <returns>The same result.</returns>
    public static async Task<Result<TIn>> Tap<TIn>(this Task<Result<TIn>> resultTask, Action<TIn> action) => (await resultTask).Tap(action);
}
