using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public sealed class CombinatorTests
{
    private static readonly ConflictError Conflict = new("Thing.Conflict", "conflict");
    private static readonly NotFoundError NotFound = new("Thing.NotFound", "missing");

    [Fact]
    public void Map_transforms_success() => Assert.Equal<Result<string>>("2", Result.Success(1).Map(x => (x + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)));

    [Fact]
    public void Map_keeps_error_and_skips_function()
    {
        Result<int> result = NotFound;

        Assert.Equal<Result<int>>(NotFound, result.Map(Fail<int, int>));
    }

    [Fact]
    public void Bind_chains_success() => Assert.Equal<Result<int>>(3, Result.Success(1).Bind(x => Result.Success(x + 2)));

    [Fact]
    public void Bind_returns_inner_error() => Assert.Equal<Result<int>>(Conflict, Result.Success(1).Bind(_ => Result.Failure<int>(Conflict)));

    [Fact]
    public void Bind_keeps_outer_error_and_skips_function()
    {
        Result<int> result = NotFound;

        Assert.Equal<Result<int>>(NotFound, result.Bind(Fail<int, Result<int>>));
    }

    [Fact]
    public void Bind_can_change_type_and_use_implicit_returns()
    {
        static Result<string> Describe(int x) => x > 0 ? "positive" : new DomainError("Number.NotPositive", "not positive");

        Assert.Equal<Result<string>>("positive", Result.Success(1).Bind(Describe));
        Assert.True(Result.Success(-1).Bind(Describe).IsFailure);
    }

    [Fact]
    public void Tap_runs_on_success_only()
    {
        List<int> seen = [];

        Result.Success(1).Tap(seen.Add);
        Result.Failure<int>(NotFound).Tap(seen.Add);

        Assert.Equal([1], seen);
    }

    [Fact]
    public void Ensure_passes_when_predicate_holds() => Assert.Equal<Result<int>>(5, Result.Success(5).Ensure(x => x > 3, Conflict));

    [Fact]
    public void Ensure_fails_when_predicate_fails() => Assert.Equal<Result<int>>(Conflict, Result.Success(1).Ensure(x => x > 3, Conflict));

    [Fact]
    public void Ensure_keeps_earlier_error_and_skips_predicate() =>
        Assert.Equal<Result<int>>(NotFound, Result.Failure<int>(NotFound).Ensure(Fail<int, bool>, Conflict));

    [Fact]
    public void MapError_replaces_error() => Assert.Equal<Result<int>>(Conflict, Result.Failure<int>(NotFound).MapError(_ => Conflict));

    [Fact]
    public void MapError_skips_success() => Assert.Equal<Result<int>>(1, Result.Success(1).MapError(Fail<Error, Error>));

    [Fact]
    public void TapError_runs_on_failure_only()
    {
        List<Error> seen = [];

        Result.Success(1).TapError(seen.Add);
        Result.Failure<int>(NotFound).TapError(seen.Add);

        Assert.Equal([NotFound], seen);
    }

    [Fact]
    public void Match_picks_the_track()
    {
        Assert.Equal("ok 1", Result.Success(1).Match(x => $"ok {x}", error => error.Code));
        Assert.Equal(NotFound.Code, Result.Failure<int>(NotFound).Match(x => $"ok {x}", error => error.Code));
    }

    private static TOut Fail<TIn, TOut>(TIn input) => throw new InvalidOperationException($"Should not be called with {input}.");
}
