namespace TrainerSphere.UnitTests.SharedKernel.Results;

/// <summary>
/// f(x) = A * x + B, generated as data so FsCheck can print and shrink counterexamples.
/// </summary>
/// <param name="A">The slope.</param>
/// <param name="B">The intercept.</param>
public sealed record LinearFunction(int A, int B)
{
    public int Invoke(int x)
    {
        // Two statements because IDE0047 (no redundant parentheses) and SA1407 (explicit precedence) conflict.
        int product = unchecked(A * x);
        return unchecked(product + B);
    }
}
