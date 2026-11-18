namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// Factory and combination helpers for <see cref="Result{T}"/>.
/// </summary>
public static class Result
{
    private static readonly Result<Unit> UnitSuccess = new Success<Unit>(default);

    /// <summary>
    /// Returns a successful result without a value.
    /// </summary>
    /// <returns>The successful result.</returns>
    public static Result<Unit> Success() => UnitSuccess;

    /// <summary>
    /// Returns a successful result with the specified value.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns>The successful result.</returns>
    public static Result<T> Success<T>(T value)
        where T : notnull =>
        new Success<T>(value);

    /// <summary>
    /// Returns a failed result with the specified error.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="error">The error.</param>
    /// <returns>The failed result.</returns>
    public static Result<T> Failure<T>(Error error)
        where T : notnull =>
        new(error ?? throw new ArgumentNullException(nameof(error)));

    /// <summary>
    /// Combines two results into a tuple. Validation errors from both are merged; otherwise the first error wins.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <returns>The combined result.</returns>
    public static Result<(T1 First, T2 Second)> Combine<T1, T2>(Result<T1> first, Result<T2> second)
        where T1 : notnull
        where T2 : notnull =>
        (first, second) switch
        {
            (Success<T1> a, Success<T2> b) => new Success<(T1, T2)>((a.Value, b.Value)),
            _ => CombineErrors([ErrorOf(first), ErrorOf(second)]),
        };

    /// <summary>
    /// Combines three results into a tuple. Validation errors are merged; otherwise the first error wins.
    /// </summary>
    /// <typeparam name="T1">The first value type.</typeparam>
    /// <typeparam name="T2">The second value type.</typeparam>
    /// <typeparam name="T3">The third value type.</typeparam>
    /// <param name="first">The first result.</param>
    /// <param name="second">The second result.</param>
    /// <param name="third">The third result.</param>
    /// <returns>The combined result.</returns>
    public static Result<(T1 First, T2 Second, T3 Third)> Combine<T1, T2, T3>(Result<T1> first, Result<T2> second, Result<T3> third)
        where T1 : notnull
        where T2 : notnull
        where T3 : notnull =>
        (first, second, third) switch
        {
            (Success<T1> a, Success<T2> b, Success<T3> c) => new Success<(T1, T2, T3)>((a.Value, b.Value, c.Value)),
            _ => CombineErrors([ErrorOf(first), ErrorOf(second), ErrorOf(third)]),
        };

    /// <summary>
    /// Combines a sequence of results into a list of values. Validation errors are merged; otherwise the first error wins.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="results">The results.</param>
    /// <returns>The combined result.</returns>
    public static Result<IReadOnlyList<T>> Combine<T>(IEnumerable<Result<T>> results)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(results);

        List<T> values = [];
        List<Error> errors = [];

        foreach (Result<T> result in results)
        {
            switch (result)
            {
                case Success<T> success:
                    values.Add(success.Value);
                    break;
                case Error error:
                    errors.Add(error);
                    break;
                case null:
                    throw ResultGuard.Uninitialized();
            }
        }

        return errors.Count == 0 ? new Success<IReadOnlyList<T>>(values) : CombineErrors(errors);
    }

    private static Error? ErrorOf<T>(Result<T> result)
        where T : notnull =>
        result switch
        {
            Success<T> => null,
            Error error => error,
            null => throw ResultGuard.Uninitialized(),
        };

    private static Error CombineErrors(IReadOnlyList<Error?> errors)
    {
        List<Error> failures = [.. errors.OfType<Error>()];

        return failures.TrueForAll(error => error is ValidationError)
            ? ValidationError.Merge(failures.Cast<ValidationError>())
            : failures[0];
    }
}
