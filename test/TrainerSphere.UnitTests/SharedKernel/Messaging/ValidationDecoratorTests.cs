using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Messaging;

public sealed class ValidationDecoratorTests
{
    [Fact]
    public async Task Invalid_command_returns_validation_error_with_camel_case_json_paths_and_skips_handler()
    {
        RecordingHandler inner = new();
        ValidationCommandDecorator<LogSetsCommand, int> decorator = new(inner, [new LogSetsValidator()]);

        Result<int> result = await decorator.Handle(new LogSetsCommand(string.Empty, [new SetInput(5), new SetInput(0), new SetInput(-1)]), CancellationToken.None);

        ValidationError error = Assert.IsType<ValidationError>(result.Value);
        Assert.Equal(["sessionName", "sets[1].reps", "sets[2].reps"], error.ToDictionary().Keys.Order(StringComparer.Ordinal));
        Assert.Equal(0, inner.Calls);
    }

    [Fact]
    public async Task Valid_command_reaches_handler()
    {
        RecordingHandler inner = new();
        ValidationCommandDecorator<LogSetsCommand, int> decorator = new(inner, [new LogSetsValidator()]);

        Result<int> result = await decorator.Handle(new LogSetsCommand("Legs", [new SetInput(5)]), CancellationToken.None);

        Assert.Equal<Result<int>>(1, result);
        Assert.Equal(1, inner.Calls);
    }

    [Fact]
    public async Task No_validators_means_valid()
    {
        RecordingHandler inner = new();
        ValidationCommandDecorator<LogSetsCommand, int> decorator = new(inner, []);

        Assert.True((await decorator.Handle(new LogSetsCommand(string.Empty, []), CancellationToken.None)).IsSuccess);
    }

    [Fact]
    public async Task Logging_decorator_passes_both_tracks_through()
    {
        LoggingCommandDecorator<LogSetsCommand, int> decorator = new(new RecordingHandler(), NullLogger<LoggingCommandDecorator<LogSetsCommand, int>>.Instance);
        LoggingCommandDecorator<LogSetsCommand, int> failing = new(new FailingHandler(), NullLogger<LoggingCommandDecorator<LogSetsCommand, int>>.Instance);

        Assert.Equal<Result<int>>(1, await decorator.Handle(new LogSetsCommand("x", []), CancellationToken.None));
        Assert.IsType<NotFoundError>((await failing.Handle(new LogSetsCommand("x", []), CancellationToken.None)).Value);
    }

    [Theory]
    [InlineData("Email", "email")]
    [InlineData("Sets[2].Reps", "sets[2].reps")]
    [InlineData("Exercises[0].Goal.MinReps", "exercises[0].goal.minReps")]
    [InlineData("", "")]
    public void Field_names_become_camel_case_json_paths(string property, string expected) =>
        Assert.Equal(expected, FieldNames.ToJsonPath(property));

    internal sealed record SetInput(int Reps);

    internal sealed record LogSetsCommand(string SessionName, IReadOnlyList<SetInput> Sets) : ICommand<int>;

    private sealed class LogSetsValidator : AbstractValidator<LogSetsCommand>
    {
        public LogSetsValidator()
        {
            RuleFor(c => c.SessionName).NotEmpty();
            RuleForEach(c => c.Sets).ChildRules(set => set.RuleFor(s => s.Reps).GreaterThan(0));
        }
    }

    private sealed class RecordingHandler : ICommandHandler<LogSetsCommand, int>
    {
        public int Calls { get; private set; }

        public Task<Result<int>> Handle(LogSetsCommand command, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult<Result<int>>(Calls);
        }
    }

    private sealed class FailingHandler : ICommandHandler<LogSetsCommand, int>
    {
        public Task<Result<int>> Handle(LogSetsCommand command, CancellationToken cancellationToken) =>
            Task.FromResult<Result<int>>(new NotFoundError("Thing.NotFound", "missing"));
    }
}
