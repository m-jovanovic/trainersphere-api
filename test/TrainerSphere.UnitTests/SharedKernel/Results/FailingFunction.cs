using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

/// <summary>
/// A function that can fail: x =&gt; x % Modulus == 0 ? Error : x + Offset.
/// </summary>
/// <param name="Modulus">Inputs divisible by this fail.</param>
/// <param name="Offset">Added to other inputs.</param>
/// <param name="Error">The error for failing inputs.</param>
public sealed record FailingFunction(int Modulus, int Offset, Error Error)
{
    public Result<int> Invoke(int x) => x % Modulus == 0 ? Error : unchecked(x + Offset);
}
