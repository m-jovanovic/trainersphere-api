using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

public sealed class AsyncCombinatorTests
{
    private static readonly NotFoundError NotFound = new("Thing.NotFound", "missing");
    private static readonly ConflictError Conflict = new("Thing.Conflict", "conflict");

    [Fact]
    public async Task Async_lambdas_bind_to_Task_overloads_on_result_and_task_receivers()
    {
        // The declared local types are the assertion: they only compile if the Task overload was chosen.
        Task<Result<int>> bound = Result.Success(1).Bind(async x =>
        {
            await Task.Yield();
            return Result.Success(x + 1);
        });
        Task<Result<int>> mapped = Result.Success(1).Map(async x =>
        {
            await Task.Yield();
            return x + 1;
        });
        Task<Result<int>> chained = Task.FromResult(Result.Success(1)).Bind(async x =>
        {
            await Task.Yield();
            return Result.Success(x + 1);
        });

        Assert.Equal<Result<int>>(2, await bound);
        Assert.Equal<Result<int>>(2, await mapped);
        Assert.Equal<Result<int>>(2, await chained);
    }

    [Fact]
    public async Task Async_lambdas_stay_on_ValueTask_for_ValueTask_receivers()
    {
        ValueTask<Result<int>> source = ValueTask.FromResult(Result.Success(1));

        ValueTask<Result<int>> bound = source.Bind(async x =>
        {
            await Task.Yield();
            return Result.Success(x + 1);
        });

        Assert.Equal<Result<int>>(2, await bound);
    }

    [Fact]
    public async Task ValueTask_returning_methods_bind_to_ValueTask_overloads()
    {
        static ValueTask<Result<int>> Increment(int x) => ValueTask.FromResult(Result.Success(x + 1));
        static ValueTask<bool> IsPositive(int x) => ValueTask.FromResult(x > 0);

        ValueTask<Result<int>> bound = Result.Success(1).Bind(Increment);
        ValueTask<Result<int>> ensured = Result.Success(1).Ensure(IsPositive, Conflict);

        Assert.Equal<Result<int>>(2, await bound);
        Assert.Equal<Result<int>>(1, await ensured);
    }

    [Fact]
    public async Task ValueTask_chain_switches_to_Task_when_a_step_returns_Task()
    {
        ValueTask<Result<int>> source = ValueTask.FromResult(Result.Success(1));

        Task<Result<int>> chained = source
            .Map(x => x + 1)
            .Bind(x => Task.FromResult(Result.Success(x * 10)));

        Assert.Equal<Result<int>>(20, await chained);
    }

    [Fact]
    public async Task Task_chain_runs_all_steps_in_order_on_success()
    {
        List<string> calls = [];

        Result<string> result = await Task.FromResult(Result.Success(1))
            .Map(x => Record(calls, "map", x + 1))
            .Bind(async x => Record(calls, "bind", Result.Success(await Task.FromResult(x * 10))))
            .Ensure(x => Task.FromResult(Record(calls, "ensure", x == 20)), Conflict)
            .Tap(_ => Task.Run(() => Record(calls, "tap-task", 0)))
            .Tap(_ => Record(calls, "tap", 0))
            .Map(x => ValueTask.FromResult(Record(calls, "map-valuetask", x.ToString(System.Globalization.CultureInfo.InvariantCulture))));

        Assert.Equal<Result<string>>("20", result);
        Assert.Equal(["map", "bind", "ensure", "tap-task", "tap", "map-valuetask"], calls);
    }

    [Fact]
    public async Task Task_chain_short_circuits_after_first_failure()
    {
        List<string> calls = [];

        Result<int> result = await Task.FromResult(Result.Success(1))
            .Bind(x => Task.FromResult(Record(calls, "first", Result.Failure<int>(NotFound))))
            .Map(x => Record(calls, "map", x))
            .Map(x => Task.FromResult(Record(calls, "map-task", x)))
            .Bind(x => Record(calls, "bind", Result.Success(x)))
            .Bind(x => ValueTask.FromResult(Record(calls, "bind-valuetask", Result.Success(x))))
            .Ensure(x => Record(calls, "ensure", true), Conflict)
            .Ensure(x => Task.FromResult(Record(calls, "ensure-task", true)), Conflict)
            .Tap(_ => Record(calls, "tap", 0))
            .Tap(_ => Task.Run(() => Record(calls, "tap-task", 0)))
            .Tap(_ => ValueTask.CompletedTask);

        Assert.Equal<Result<int>>(NotFound, result);
        Assert.Equal(["first"], calls);
    }

    [Fact]
    public async Task ValueTask_chain_short_circuits_after_first_failure()
    {
        List<string> calls = [];

        Result<int> result = await ValueTask.FromResult(Result.Failure<int>(NotFound))
            .Map(x => Record(calls, "map", x))
            .Map(x => ValueTask.FromResult(Record(calls, "map-valuetask", x)))
            .Bind(x => ValueTask.FromResult(Record(calls, "bind-valuetask", Result.Success(x))))
            .Ensure(x => ValueTask.FromResult(Record(calls, "ensure-valuetask", true)), Conflict)
            .Tap(_ => Record(calls, "tap", 0))
            .Tap(_ =>
            {
                Record(calls, "tap-valuetask", 0);
                return ValueTask.CompletedTask;
            });

        Assert.Equal<Result<int>>(NotFound, result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Ensure_failure_stops_later_async_steps()
    {
        List<string> calls = [];

        Result<int> result = await Result.Success(1)
            .Ensure(_ => Task.FromResult(false), Conflict)
            .Bind(x => Task.FromResult(Record(calls, "bind", Result.Success(x))))
            .Tap(_ => Task.Run(() => Record(calls, "tap", 0)));

        Assert.Equal<Result<int>>(Conflict, result);
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Async_TapError_lambda_is_awaited_not_async_void()
    {
        bool logged = false;

        Result<int> result = await Task.FromResult(Result.Failure<int>(NotFound)).TapError(async _ =>
        {
            await Task.Delay(20);
            logged = true;
        });

        Assert.True(logged);
        Assert.Equal<Result<int>>(NotFound, result);
    }

    [Fact]
    public async Task MapError_TapError_and_Match_work_on_async_receivers()
    {
        List<Error> seen = [];

        string fromTask = await Task.FromResult(Result.Failure<int>(NotFound))
            .MapError(_ => Conflict)
            .TapError(seen.Add)
            .Match(x => $"ok {x}", error => error.Code);
        string fromValueTask = await ValueTask.FromResult(Result.Success(7))
            .MapError(_ => Conflict)
            .TapError(seen.Add)
            .Match(x => $"ok {x}", error => error.Code);

        Assert.Equal(Conflict.Code, fromTask);
        Assert.Equal("ok 7", fromValueTask);
        Assert.Equal([Conflict], seen);
    }

    [Fact]
    public async Task Async_TapError_on_ValueTask_and_result_receivers_runs_only_on_failure()
    {
        int calls = 0;

        await ValueTask.FromResult(Result.Failure<int>(NotFound)).TapError(_ => Task.Run(() => calls++));
        await Result.Failure<int>(NotFound).TapError(_ => Task.Run(() => calls++));
        await Result.Success(1).TapError(_ => Task.Run(() => calls++));

        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Async_steps_propagate_exceptions_instead_of_converting_them()
    {
        Task<Result<int>> chain = Result.Success(1).Bind(Throw);

        await Assert.ThrowsAsync<TimeoutException>(() => chain);

        static Task<Result<int>> Throw(int value) => throw new TimeoutException(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Cancellation_propagates_through_the_chain()
    {
        using CancellationTokenSource cts = new();
        await cts.CancelAsync();

        Task<Result<int>> chain = Result.Success(1).Bind(async x =>
        {
            await Task.Delay(1000, cts.Token);
            return Result.Success(x);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => chain);
    }

    private static T Record<T>(List<string> calls, string name, T value)
    {
        calls.Add(name);
        return value;
    }
}
