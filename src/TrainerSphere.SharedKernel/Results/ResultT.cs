namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The outcome of an operation: either a <see cref="Success{T}"/> carrying a value, or an <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public union Result<T>(Success<T>, Error) : IEquatable<Result<T>>
    where T : notnull
{
    /// <summary>
    /// Gets a value indicating whether the result is a success.
    /// </summary>
    public bool IsSuccess => Value is Success<T>;

    /// <summary>
    /// Gets a value indicating whether the result is a failure.
    /// </summary>
    public bool IsFailure => Value is Error;

    // Declared so 'return value;' works in handlers. For Result<SomeError> this operator wins over the
    // union conversion, so an Error assigned to Result<Error> becomes a success; never use an Error as T.
    public static implicit operator Result<T>(T value) => new Success<T>(value);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    /// <inheritdoc />
    public bool Equals(Result<T> other) => Equals(Value, other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    /// <inheritdoc />
    public override string ToString() => Value?.ToString() ?? "Result { default }";
}
