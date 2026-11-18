namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The success case of <see cref="Result{T}"/>.
/// </summary>
/// <typeparam name="T">The value type.</typeparam>
/// <param name="Value">The success value.</param>
public sealed record Success<T>(T Value)
    where T : notnull
{
    public T Value { get; } = Value ?? throw new ArgumentNullException(nameof(Value));
}
