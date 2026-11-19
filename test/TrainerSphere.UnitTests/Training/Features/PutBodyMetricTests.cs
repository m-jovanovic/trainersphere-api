using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features;
using TrainerSphere.Api.Modules.Training.Features.BodyMetrics;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class PutBodyMetricTests
{
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IBodyMetricRepository _metrics = Substitute.For<IBodyMetricRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(TrainingTestData.Now, TimeSpan.Zero));

    [Fact]
    public async Task A_future_date_is_a_validation_error_on_the_date()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<BodyMetricResponse> result = await Handler().Handle(
            Command(Viewer.Client(client.Id, client.TrainerId), client.Id, TrainingTestData.Today.AddDays(1)),
            CancellationToken.None);

        ValidationError error = result.Value.ShouldBeOfType<ValidationError>();
        error.Errors.ShouldBe([new FieldError("date", ValidationError.ValidationCode, "Measurements cannot be recorded for a future date.")]);
        await _metrics.DidNotReceive().UpsertAsync(Arg.Any<BodyMetric>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_client_records_their_own_measurement()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<BodyMetricResponse> result = await Handler().Handle(
            Command(Viewer.Client(client.Id, client.TrainerId), client.Id, TrainingTestData.Today),
            CancellationToken.None);

        BodyMetricResponse response = result.Value.ShouldBeOfType<Success<BodyMetricResponse>>().Value;
        response.RecordedBy.ShouldBe(RecordedBy.Client);
        response.WeightKg.ShouldBe(80.5m);
        response.UpdatedAt.ShouldBe(TrainingTestData.Now);
        await _metrics.Received(1).UpsertAsync(
            Arg.Is<BodyMetric>(m => m.ClientId == client.Id && m.Date == TrainingTestData.Today && m.RecordedBy == RecordedBy.Client),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task A_trainer_records_a_measurement_for_their_client()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<BodyMetricResponse> result = await Handler().Handle(
            Command(Viewer.Trainer(TrainingTestData.TrainerId), client.Id, TrainingTestData.Today),
            CancellationToken.None);

        result.Value.ShouldBeOfType<Success<BodyMetricResponse>>().Value.RecordedBy.ShouldBe(RecordedBy.Trainer);
    }

    [Fact]
    public async Task Another_clients_metrics_are_not_found()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<BodyMetricResponse> result = await Handler().Handle(
            Command(Viewer.Client(Guid.NewGuid(), Guid.NewGuid()), client.Id, TrainingTestData.Today),
            CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Client.NotFound");
    }

    [Fact]
    public void The_validator_requires_at_least_one_measurement()
    {
        PutBodyMetricValidator validator = new();
        PutBodyMetricCommand empty = Command(Viewer.Client(TrainingTestData.ClientId, TrainingTestData.TrainerId), TrainingTestData.ClientId, TrainingTestData.Today) with
        {
            WeightKg = null,
        };

        validator.Validate(empty).Errors.ShouldContain(f => f.PropertyName == PutBodyMetricValidator.MeasurementsField);
        validator.Validate(Command(Viewer.Client(TrainingTestData.ClientId, TrainingTestData.TrainerId), TrainingTestData.ClientId, TrainingTestData.Today)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void The_validator_enforces_the_measurement_ranges()
    {
        PutBodyMetricValidator validator = new();
        PutBodyMetricCommand command = Command(Viewer.Client(TrainingTestData.ClientId, TrainingTestData.TrainerId), TrainingTestData.ClientId, TrainingTestData.Today) with
        {
            WeightKg = 1m,
            RestingHeartRate = 300,
        };

        FluentValidation.Results.ValidationResult result = validator.Validate(command);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(f => f.PropertyName == "WeightKg");
        result.Errors.ShouldContain(f => f.PropertyName == "RestingHeartRate");
    }

    private static PutBodyMetricCommand Command(Viewer viewer, Guid clientId, DateOnly date) =>
        new(viewer, clientId, date, 80.5m, null, null, null, null, null, null, null, null);

    private PutBodyMetricHandler Handler() => new(_clients, _metrics, _time);
}
