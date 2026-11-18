namespace TrainerSphere.SharedKernel.Results;

/// <summary>
/// The value of a successful operation that produces no data; <c>Result&lt;Unit&gt;</c> replaces a non-generic result.
/// </summary>
public readonly record struct Unit
{
    public override string ToString() => "()";
}
