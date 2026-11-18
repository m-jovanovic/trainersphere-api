using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features.Progress;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class ProgressSummaryTests
{
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IAssignmentRepository _assignments = Substitute.For<IAssignmentRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(TrainingTestData.Now, TimeSpan.Zero));

    [Fact]
    public async Task The_summary_composes_adherence_streak_and_weeks()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _sessions.GetCompletedAsync(client.Id, Arg.Any<CancellationToken>()).Returns(
            [
                new SessionVolume(Guid.NewGuid(), new DateTime(2026, 11, 17, 18, 0, 0, DateTimeKind.Utc), 1000m, 10),
                new SessionVolume(Guid.NewGuid(), new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc), 2000m, 20),
            ]);

        Assignment done = TrainingTestData.ScheduledAssignment(client, new DateOnly(2026, 11, 17));
        done.Complete(TrainingTestData.Now, TrainingTestData.Now);
        Assignment missed = TrainingTestData.ScheduledAssignment(client, new DateOnly(2026, 11, 16));
        Assignment today = TrainingTestData.ScheduledAssignment(client, TrainingTestData.Today);
        _assignments.ListAsync(null, client.Id, Arg.Any<DateOnly>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>())
            .Returns([done, missed, today]);

        var handler = new ProgressSummaryHandler(_clients, _sessions, _assignments, _time);

        Result<ProgressSummaryResponse> result = await handler.Handle(
            new ProgressSummaryQuery(Viewer.Trainer(TrainingTestData.TrainerId), client.Id, 4),
            CancellationToken.None);

        ProgressSummaryResponse summary = result.Value.ShouldBeOfType<Success<ProgressSummaryResponse>>().Value;
        summary.TotalSessions.ShouldBe(2);
        summary.Adherence7d.ShouldBe(0.5m);
        summary.Adherence28d.ShouldBe(0.5m);
        summary.StreakWeeks.ShouldBe(2);
        summary.Weeks.Count.ShouldBe(4);
        summary.Weeks[3].WeekStart.ShouldBe(new DateOnly(2026, 11, 16));
        summary.Weeks[3].Sessions.ShouldBe(1);
        summary.Weeks[3].VolumeKg.ShouldBe(1000m);
        summary.Weeks[2].Sessions.ShouldBe(1);
        summary.Weeks[1].Sessions.ShouldBe(0);
    }

    [Fact]
    public async Task Another_clients_summary_is_not_found()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new ProgressSummaryHandler(_clients, _sessions, _assignments, _time);

        Result<ProgressSummaryResponse> result = await handler.Handle(
            new ProgressSummaryQuery(Viewer.Client(Guid.NewGuid(), Guid.NewGuid()), client.Id, 12),
            CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Client.NotFound");
    }
}
