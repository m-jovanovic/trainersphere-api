using FsCheck.Xunit;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

[Properties(Arbitrary = [typeof(Arbitraries)], MaxTest = 500)]
public sealed class LawTests
{
    [Property]
    public bool Functor_identity(Result<int> result) => result.Map(x => x) == result;

    [Property]
    public bool Functor_composition(Result<int> result, LinearFunction f, LinearFunction g) =>
        result.Map(f.Invoke).Map(g.Invoke) == result.Map(x => g.Invoke(f.Invoke(x)));

    [Property]
    public bool Monad_left_identity(int value, FailingFunction f) => Result.Success(value).Bind(f.Invoke) == f.Invoke(value);

    [Property]
    public bool Monad_right_identity(Result<int> result) => result.Bind(Result.Success) == result;

    [Property]
    public bool Monad_associativity(Result<int> result, FailingFunction f, FailingFunction g) =>
        result.Bind(f.Invoke).Bind(g.Invoke) == result.Bind(x => f.Invoke(x).Bind(g.Invoke));

    [Property]
    public bool Map_is_bind_of_success(Result<int> result, LinearFunction f) =>
        result.Map(f.Invoke) == result.Bind(x => Result.Success(f.Invoke(x)));

    [Property]
    public bool Ensure_is_bind_of_predicate(Result<int> result, int threshold, Error error) =>
        result.Ensure(x => x > threshold, error) == result.Bind(x => x > threshold ? Result.Success(x) : error);

    [Property]
    public bool MapError_identity(Result<int> result) => result.MapError(error => error) == result;

    [Property]
    public bool MapError_composition(Result<int> result, Error first, Error second) =>
        result.MapError(_ => first).MapError(_ => second) == result.MapError(_ => second);

    [Property]
    public bool MapError_never_touches_success(int value, Error error) =>
        Result.Success(value).MapError(_ => error) == Result.Success(value);

    [Property]
    public bool Match_with_constructors_round_trips(Result<int> result) =>
        result.Match(Result.Success, Result.Failure<int>) == result;

    [Property]
    public bool Tap_returns_same_result_and_runs_only_on_success(Result<int> result)
    {
        int calls = 0;

        Result<int> tapped = result.Tap(_ => calls++);

        return tapped == result && calls == (result.IsSuccess ? 1 : 0);
    }

    [Property]
    public bool TapError_returns_same_result_and_runs_only_on_failure(Result<int> result)
    {
        int calls = 0;

        Result<int> tapped = result.TapError(_ => calls++);

        return tapped == result && calls == (result.IsFailure ? 1 : 0);
    }

    [Property]
    public async Task<bool> Task_bind_agrees_with_sync_bind(Result<int> result, FailingFunction f) =>
        await result.Bind(x => Task.FromResult(f.Invoke(x))) == result.Bind(f.Invoke)
        && await Task.FromResult(result).Bind(f.Invoke) == result.Bind(f.Invoke)
        && await Task.FromResult(result).Bind(x => Task.FromResult(f.Invoke(x))) == result.Bind(f.Invoke);

    [Property]
    public async Task<bool> ValueTask_bind_agrees_with_sync_bind(Result<int> result, FailingFunction f) =>
        await result.Bind(x => ValueTask.FromResult(f.Invoke(x))) == result.Bind(f.Invoke)
        && await ValueTask.FromResult(result).Bind(f.Invoke) == result.Bind(f.Invoke)
        && await ValueTask.FromResult(result).Bind(x => ValueTask.FromResult(f.Invoke(x))) == result.Bind(f.Invoke);

    [Property]
    public async Task<bool> Async_map_agrees_with_sync_map(Result<int> result, LinearFunction f) =>
        await result.Map(x => Task.FromResult(f.Invoke(x))) == result.Map(f.Invoke)
        && await result.Map(x => ValueTask.FromResult(f.Invoke(x))) == result.Map(f.Invoke)
        && await Task.FromResult(result).Map(f.Invoke) == result.Map(f.Invoke)
        && await ValueTask.FromResult(result).Map(f.Invoke) == result.Map(f.Invoke);

    [Property]
    public async Task<bool> Async_ensure_agrees_with_sync_ensure(Result<int> result, int threshold, Error error) =>
        await result.Ensure(x => Task.FromResult(x > threshold), error) == result.Ensure(x => x > threshold, error)
        && await result.Ensure(x => ValueTask.FromResult(x > threshold), error) == result.Ensure(x => x > threshold, error);

    [Property]
    public bool Combine_succeeds_only_when_both_succeed(Result<int> first, Result<int> second) =>
        Result.Combine(first, second).IsSuccess == (first.IsSuccess && second.IsSuccess);

    [Property]
    public bool Combine_of_list_preserves_order(int[] values) =>
        Result.Combine(values.Select(Result.Success)).Match(list => list.SequenceEqual(values), _ => false);
}
