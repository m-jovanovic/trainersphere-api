namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The outcome of an operation: either a <see cref="Success{T}"/> carrying a value, or an <see cref="Error"/>.
/// </summary>
/// <typeparam name="T">The success value type.</typeparam>
public union Result<T>(Success<T>, Error) : IEquatable<Result<T>>
    where T : notnull
{
    public bool IsSuccess => Value is Success<T>;

    public bool IsFailure => Value is Error;

    // Lets handlers write 'return value;'. Never instantiate T with an Error type: this operator would win.
    public static implicit operator Result<T>(T value) => new Success<T>(value);

    public static bool operator ==(Result<T> left, Result<T> right) => left.Equals(right);

    public static bool operator !=(Result<T> left, Result<T> right) => !left.Equals(right);

    public bool Equals(Result<T> other) => Equals(Value, other.Value);

    public override bool Equals(object? obj) => obj is Result<T> other && Equals(other);

    public override int GetHashCode() => Value?.GetHashCode() ?? 0;

    public override string ToString() => Value?.ToString() ?? "Result { default }";
}
