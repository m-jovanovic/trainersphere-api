namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// Factory helpers for <see cref="Result{T}"/>.
/// </summary>
public static class Result
{
    private static readonly Result<Unit> UnitSuccess = new Success<Unit>(default);

    public static Result<Unit> Success() => UnitSuccess;

    public static Result<T> Success<T>(T value)
        where T : notnull =>
        new Success<T>(value);

    public static Result<T> Failure<T>(Error error)
        where T : notnull =>
        new(error ?? throw new ArgumentNullException(nameof(error)));

    public static Result<Unit> Failure(Error error) => Failure<Unit>(error);

    public static Result<T> FromNullable<T>(T? value, Error whenNull)
        where T : class =>
        value is null ? Failure<T>(whenNull) : Success(value);

    public static Result<T> FromNullable<T>(T? value, Error whenNull)
        where T : struct =>
        value is { } v ? Success(v) : Failure<T>(whenNull);

    /// <summary>
    /// Returns success when the condition holds, otherwise the error.
    /// </summary>
    public static Result<Unit> Require(bool condition, Error error) => condition ? UnitSuccess : Failure(error);

    /// <summary>
    /// Combines two results. Validation errors from both sides are merged; otherwise the first error wins.
    /// </summary>
    public static Result<(T1 First, T2 Second)> Combine<T1, T2>(Result<T1> first, Result<T2> second)
        where T1 : notnull
        where T2 : notnull =>
        (first, second) switch
        {
            (Success<T1> a, Success<T2> b) => Success((a.Value, b.Value)),
            (ValidationError a, ValidationError b) => Failure<(T1, T2)>(a.Merge(b)),
            (Error a, _) => Failure<(T1, T2)>(a),
            (_, Error b) => Failure<(T1, T2)>(b),
        };

    /// <summary>
    /// Collects all values, or returns the first error (merging every validation error encountered).
    /// </summary>
    public static Result<IReadOnlyList<T>> Sequence<T>(IEnumerable<Result<T>> results)
        where T : notnull
    {
        ArgumentNullException.ThrowIfNull(results);

        List<T> values = [];
        ValidationError? validation = null;
        Error? firstError = null;

        foreach (Result<T> result in results)
        {
            switch (result)
            {
                case Success<T> success:
                    values.Add(success.Value);
                    break;
                case ValidationError v:
                    validation = validation is null ? v : validation.Merge(v);
                    break;
                case Error e:
                    firstError ??= e;
                    break;
            }
        }

        if (firstError is not null)
        {
            return Failure<IReadOnlyList<T>>(firstError);
        }

        return validation is not null ? Failure<IReadOnlyList<T>>(validation) : Success<IReadOnlyList<T>>(values);
    }
}
