using FsCheck;
using FsCheck.Fluent;
using TrainerSphere.SharedKernel.Results;
using Result = TrainerSphere.SharedKernel.Results.Result;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public static class Arbitraries
{
    public static Gen<Error> ErrorGen { get; } =
        Gen.Elements("A", "B", "C")
            .SelectMany(
                code => Gen.Elements<Error>(
                    new NotFoundError(code, "not found"),
                    new ConflictError(code, "conflict"),
                    new DomainError(code, "domain"),
                    new UnauthorizedError(code, "unauthorized"),
                    new ForbiddenError(code, "forbidden"),
                    new UnexpectedError(code, "unexpected"),
                    new ValidationError(new FieldError("field" + code, code, "invalid"))),
                (_, error) => error);

    public static Gen<Result<int>> ResultGen { get; } =
        Gen.OneOf(
            ArbMap.Default.GeneratorFor<int>().Select(value => Result.Success(value)),
            ErrorGen.Select(error => Result.Failure<int>(error)));

    public static Arbitrary<Error> ErrorArbitrary() => Arb.From(ErrorGen);

    public static Arbitrary<Result<int>> ResultArbitrary() => Arb.From(ResultGen);

    public static Arbitrary<LinearFunction> Linear() =>
        Arb.From(
            from a in Gen.Choose(-5, 5)
            from b in Gen.Choose(-100, 100)
            select new LinearFunction(a, b));

    public static Arbitrary<FailingFunction> Failing() =>
        Arb.From(
            from modulus in Gen.Choose(2, 5)
            from offset in Gen.Choose(-10, 10)
            from error in ErrorGen
            select new FailingFunction(modulus, offset, error));
}
