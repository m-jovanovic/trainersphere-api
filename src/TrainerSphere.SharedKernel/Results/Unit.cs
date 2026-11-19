namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The value of a successful operation that produces no data; <c>Result&lt;Unit&gt;</c> replaces the non-generic result.
/// </summary>
public readonly record struct Unit
{
    /// <inheritdoc />
    public override string ToString() => "()";
}
