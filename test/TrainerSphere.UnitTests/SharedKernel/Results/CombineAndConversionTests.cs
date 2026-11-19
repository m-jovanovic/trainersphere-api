using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public sealed class CombineAndConversionTests
{
    private static readonly ValidationError EmailInvalid = new(new FieldError("email", "Email.Invalid", "bad email"));
    private static readonly ValidationError NameMissing = new(new FieldError("name", "Name.Required", "name required"));
    private static readonly NotFoundError NotFound = new("Thing.NotFound", "missing");

    [Fact]
    public void Combine_two_successes_into_tuple() =>
        Assert.Equal<Result<(int, string)>>((1, "a"), Result.Combine(Result.Success(1), Result.Success("a")));

    [Fact]
    public void Combine_three_successes_into_tuple() =>
        Assert.Equal<Result<(int, string, bool)>>((1, "a", true), Result.Combine(Result.Success(1), Result.Success("a"), Result.Success(true)));

    [Fact]
    public void Combine_merges_validation_errors()
    {
        Result<(int, string, bool)> result = Result.Combine(Result.Failure<int>(EmailInvalid), Result.Success("a"), Result.Failure<bool>(NameMissing));

        ValidationError error = Assert.IsType<ValidationError>(result.Value);
        Assert.Equal([.. EmailInvalid.Errors, .. NameMissing.Errors], error.Errors);
    }

    [Fact]
    public void Combine_returns_first_error_when_not_all_are_validation_errors()
    {
        Assert.Equal<Result<(int, int)>>(NotFound, Result.Combine(Result.Failure<int>(NotFound), Result.Failure<int>(EmailInvalid)));
        Assert.Equal<Result<(int, int)>>(EmailInvalid, Result.Combine(Result.Failure<int>(EmailInvalid), Result.Failure<int>(NotFound)));
    }

    [Fact]
    public void Combine_sequence_collects_values_or_merges_errors()
    {
        Assert.Equal([1, 2, 3], Assert.IsType<Success<IReadOnlyList<int>>>(Result.Combine([Result.Success(1), Result.Success(2), Result.Success(3)]).Value).Value);
        Assert.Equal(2, Assert.IsType<ValidationError>(Result.Combine([Result.Failure<int>(EmailInvalid), Result.Success(1), Result.Failure<int>(NameMissing)]).Value).Errors.Count);
        Assert.Equal<Result<IReadOnlyList<int>>>(NotFound, Result.Combine([Result.Success(1), Result.Failure<int>(NotFound)]));
    }

    [Fact]
    public void ValidationError_requires_field_errors() => Assert.Throws<ArgumentException>(() => new ValidationError());

    [Fact]
    public void ValidationError_equality_compares_field_errors()
    {
        Assert.Equal(new ValidationError(new FieldError("a", "b", "c")), new ValidationError(new FieldError("a", "b", "c")));
        Assert.Equal(new ValidationError(new FieldError("a", "b", "c")).GetHashCode(), new ValidationError(new FieldError("a", "b", "c")).GetHashCode());
        Assert.NotEqual(new ValidationError(new FieldError("a", "b", "c")), new ValidationError(new FieldError("a", "b", "x")));
    }

    [Fact]
    public void ValidationError_groups_messages_by_field()
    {
        ValidationError error = new(
            new FieldError("email", "Email.Required", "required"),
            new FieldError("email", "Email.Invalid", "invalid"),
            new FieldError("name", "Name.Required", "name required"));

        var grouped = error.ToDictionary();

        Assert.Equal(["required", "invalid"], grouped["email"]);
        Assert.Equal(["name required"], grouped["name"]);
        Assert.Equal(ValidationError.ValidationCode, error.Code);
    }

    [Fact]
    public void ToResult_maps_null_reference_to_error()
    {
        string? missing = null;

        Assert.Equal<Result<string>>(NotFound, missing.ToResult(NotFound));
        Assert.Equal<Result<string>>("x", "x".ToResult(NotFound));
    }

    [Fact]
    public void ToResult_maps_null_value_type_to_error()
    {
        int? missing = null;
        int? present = 3;

        Assert.Equal<Result<int>>(NotFound, missing.ToResult(NotFound));
        Assert.Equal<Result<int>>(3, present.ToResult(NotFound));
    }

    [Fact]
    public async Task ToResult_on_tasks_awaits_the_lookup()
    {
        Assert.Equal<Result<string>>(NotFound, await Task.FromResult<string?>(null).ToResult(NotFound));
        Assert.Equal<Result<string>>("x", await Task.FromResult<string?>("x").ToResult(NotFound));
        Assert.Equal<Result<int>>(NotFound, await Task.FromResult<int?>(null).ToResult(NotFound));
        Assert.Equal<Result<int>>(4, await Task.FromResult<int?>(4).ToResult(NotFound));
    }
}
