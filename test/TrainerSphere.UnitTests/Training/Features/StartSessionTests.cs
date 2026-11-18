using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features.Sessions;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class StartSessionTests
{
    private readonly ISessionRepository _sessions = Substitute.For<ISessionRepository>();
    private readonly IAssignmentRepository _assignments = Substitute.For<IAssignmentRepository>();
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IExerciseRepository _exercises = Substitute.For<IExerciseRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(TrainingTestData.Now, TimeSpan.Zero));

    public StartSessionTests()
    {
        _sessions.GetSetsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _sessions.GetPersonalRecordsAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns([]);
        _sessions.GetLastPerformanceAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>()).Returns([]);
    }

    [Fact]
    public async Task Replaying_a_create_returns_the_existing_session_without_creating()
    {
        ClientProfile client = TrainingTestData.Client();
        var sessionId = Guid.NewGuid();
        var existing = Session.Start(sessionId, client, assignment: null, "Lower A", TrainingTestData.Now, TrainingTestData.Now);
        _sessions.GetAsync(sessionId, false, Arg.Any<CancellationToken>()).Returns(existing);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, sessionId, null, "Lower A", null),
            CancellationToken.None);

        StartedSession started = result.Value.ShouldBeOfType<Success<StartedSession>>().Value;
        started.Created.ShouldBeFalse();
        started.Session.Id.ShouldBe(sessionId);
        await _sessions.DidNotReceive().AddAsync(Arg.Any<Session>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task An_id_used_by_another_client_is_a_conflict()
    {
        var sessionId = Guid.NewGuid();
        var existing = Session.Start(sessionId, TrainingTestData.Client(id: Guid.NewGuid()), assignment: null, "Lower A", TrainingTestData.Now, TrainingTestData.Now);
        _sessions.GetAsync(sessionId, false, Arg.Any<CancellationToken>()).Returns(existing);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(TrainingTestData.ClientId, sessionId, null, "Lower A", null),
            CancellationToken.None);

        result.Value.ShouldBeOfType<ConflictError>().Code.ShouldBe("Session.IdInUse");
    }

    [Fact]
    public async Task An_archived_client_cannot_start_a_session()
    {
        ClientProfile client = TrainingTestData.Client(status: ClientStatus.Archived);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, null, null, "Morning run", null),
            CancellationToken.None);

        result.Value.ShouldBeOfType<DomainError>().Code.ShouldBe("Client.Archived");
    }

    [Fact]
    public async Task Starting_ad_hoc_creates_the_session()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, null, null, "Morning run", null),
            CancellationToken.None);

        StartedSession started = result.Value.ShouldBeOfType<Success<StartedSession>>().Value;
        started.Created.ShouldBeTrue();
        started.Session.WorkoutName.ShouldBe("Morning run");
        started.Session.AssignmentId.ShouldBeNull();
        await _sessions.Received(1).AddAsync(Arg.Is<Session>(s => s.ClientId == client.Id && s.WorkoutName == "Morning run"), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_from_an_assignment_links_both_and_takes_its_name()
    {
        ClientProfile client = TrainingTestData.Client();
        Assignment assignment = TrainingTestData.ScheduledAssignment(client);
        _sessions.GetAsync(Arg.Any<Guid>(), false, Arg.Any<CancellationToken>()).Returns((Session?)null);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _assignments.GetAsync(assignment.Id, true, Arg.Any<CancellationToken>()).Returns(assignment);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, Guid.NewGuid(), assignment.Id, null, null),
            CancellationToken.None);

        StartedSession started = result.Value.ShouldBeOfType<Success<StartedSession>>().Value;
        started.Session.AssignmentId.ShouldBe(assignment.Id);
        started.Session.WorkoutName.ShouldBe("Lower A");
        await _assignments.Received(1).UpdateAsync(Arg.Is<Assignment>(a => a.SessionId == started.Session.Id), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Starting_from_an_assignment_that_already_has_a_session_is_a_conflict()
    {
        ClientProfile client = TrainingTestData.Client();
        Assignment assignment = TrainingTestData.ScheduledAssignment(client);
        var existing = Guid.NewGuid();
        assignment.StartSession(existing, TrainingTestData.Now);
        _sessions.GetAsync(Arg.Any<Guid>(), false, Arg.Any<CancellationToken>()).Returns((Session?)null);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _assignments.GetAsync(assignment.Id, true, Arg.Any<CancellationToken>()).Returns(assignment);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, Guid.NewGuid(), assignment.Id, null, null),
            CancellationToken.None);

        ConflictError error = result.Value.ShouldBeOfType<ConflictError>();
        error.Code.ShouldBe("Session.AlreadyStartedForAssignment");
        error.Extensions.ShouldNotBeNull()["sessionId"].ShouldBe(existing);
    }

    [Fact]
    public async Task Another_clients_assignment_is_not_found()
    {
        ClientProfile client = TrainingTestData.Client();
        Assignment assignment = TrainingTestData.ScheduledAssignment(TrainingTestData.Client(id: Guid.NewGuid()));
        _sessions.GetAsync(Arg.Any<Guid>(), false, Arg.Any<CancellationToken>()).Returns((Session?)null);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _assignments.GetAsync(assignment.Id, true, Arg.Any<CancellationToken>()).Returns(assignment);

        Result<StartedSession> result = await Handler().Handle(
            new StartSessionCommand(client.Id, Guid.NewGuid(), assignment.Id, null, null),
            CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Assignment.NotFound");
    }

    private StartSessionHandler Handler() => new(_sessions, _assignments, _clients, _exercises, _time);
}
