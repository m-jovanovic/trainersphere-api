using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public sealed class KernelAdditionsTests
{
    [Fact]
    public void ValidationError_For_creates_single_field_error()
    {
        var error = ValidationError.For("sets[2].reps", "Reps must be positive.");

        FieldError field = Assert.Single(error.Errors);
        Assert.Equal(new FieldError("sets[2].reps", ValidationError.ValidationCode, "Reps must be positive."), field);
        Assert.Equal("Validation", error.Code);
    }

    [Fact]
    public void ToUnit_discards_value_and_keeps_error()
    {
        NotFoundError notFound = new("X", "y");

        Assert.Equal(Result.Success(), Result.Success(5).ToUnit());
        Assert.Equal<Result<Unit>>(notFound, Result.Failure<int>(notFound).ToUnit());
    }
}
