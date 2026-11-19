using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public sealed class ResultTests
{
    private static readonly NotFoundError NotFound = new("Thing.NotFound", "missing");

    [Fact]
    public void Value_converts_implicitly_to_success()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal(new Success<int>(42), result.Value);
    }

    [Fact]
    public void Error_subtype_converts_implicitly_to_failure()
    {
        Result<int> result = NotFound;

        Assert.True(result.IsFailure);
        Assert.Same(NotFound, result.Value);
    }

    [Fact]
    public void Factories_match_implicit_conversions()
    {
        Assert.Equal<Result<int>>(42, Result.Success(42));
        Assert.Equal<Result<int>>(NotFound, Result.Failure<int>(NotFound));
        Assert.Equal(new Success<Unit>(default), Result.Success().Value);
    }

    [Fact]
    public void Failure_rejects_null_error() =>
        Assert.Throws<ArgumentNullException>(() => Result.Failure<int>(null!));

    [Fact]
    public void Success_rejects_null_value()
    {
        string? missing = null;

        Assert.Throws<ArgumentNullException>(() => Result.Success(missing!));
    }

    [Fact]
    public void Interface_typed_value_converts_implicitly()
    {
        Result<IReadOnlyList<int>> result = new List<int> { 1, 2 };

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Error_as_value_type_becomes_success_pitfall()
    {
        // Pins a documented pitfall: for Result<Error> the user-defined T conversion beats the union conversion.
        Result<Error> result = NotFound;

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Equality_is_structural_over_the_case_value()
    {
        Result<int> a = new NotFoundError("X", "y");
        Result<int> b = new NotFoundError("X", "y");

        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual<Result<int>>(1, 2);
        Assert.NotEqual<Result<int>>(new NotFoundError("X", "y"), new ConflictError("X", "y"));
        Assert.False(a.Equals((object)"not a result"));
    }

    [Fact]
    public void ToString_shows_the_case()
    {
        Assert.Equal("Success { Value = 3 }", Result.Success(3).ToString());
        Assert.Equal("Result { default }", default(Result<int>).ToString());
    }

    [Fact]
    public void Pattern_matching_tests_the_contents()
    {
        Result<int> result = 5;

        Assert.True(result is Success<int> { Value: > 3 });
        Assert.False(result is Error);
        Assert.True(result is not null);
    }

    [Fact]
    public void Nested_patterns_reach_error_subtypes()
    {
        static string Describe(Result<int> result) =>
            result switch
            {
                Success<int> success => $"ok {success.Value}",
                ValidationError validation => $"invalid {validation.Errors.Count}",
                NotFoundError => "404",
                ConflictError => "409",
                DomainError => "400",
                UnauthorizedError => "401",
                ForbiddenError => "403",
                UnexpectedError => "500",
            };

        Assert.Equal("ok 1", Describe(1));
        Assert.Equal("404", Describe(NotFound));
        Assert.Equal("invalid 1", Describe(new ValidationError(new FieldError("f", "c", "m"))));
    }

    [Fact]
    public void Default_result_is_neither_case()
    {
        Result<int> result = default;

        Assert.Null(result.Value);
        Assert.False(result.IsSuccess);
        Assert.False(result.IsFailure);
    }

    [Fact]
    public void Default_result_throws_from_an_exhaustive_switch_without_null_arm()
    {
        static string Describe(Result<int> result) =>
            result switch
            {
                Success<int> => "ok",
                Error => "error",
            };

        Assert.Throws<System.Runtime.CompilerServices.SwitchExpressionException>(() => Describe(default));
    }

    [Fact]
    public void Default_result_throws_from_every_combinator()
    {
        Result<int> result = default;

        Assert.Throws<InvalidOperationException>(() => result.Map(x => x));
        Assert.Throws<InvalidOperationException>(() => result.Bind(Result.Success));
        Assert.Throws<InvalidOperationException>(() => result.Tap(_ => { }));
        Assert.Throws<InvalidOperationException>(() => result.Ensure(_ => true, NotFound));
        Assert.Throws<InvalidOperationException>(() => result.MapError(error => error));
        Assert.Throws<InvalidOperationException>(() => result.TapError(_ => { }));
        Assert.Throws<InvalidOperationException>(() => result.Match(_ => 0, _ => 1));
        Assert.Throws<InvalidOperationException>(() => Result.Combine(result, result));
        Assert.Throws<InvalidOperationException>(() => Result.Combine([result]));
    }

    [Fact]
    public async Task Default_result_throws_from_async_combinators()
    {
        Result<int> result = default;

        await Assert.ThrowsAsync<InvalidOperationException>(() => result.Bind(x => Task.FromResult(Result.Success(x))));
        await Assert.ThrowsAsync<InvalidOperationException>(async () => await result.Bind(x => ValueTask.FromResult(Result.Success(x))));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Task.FromResult(result).Map(x => x));
    }
}
