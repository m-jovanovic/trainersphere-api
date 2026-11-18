using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features;
using TrainerSphere.Api.Modules.Training.Features.Assignments;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class CreateAssignmentTests
{
    private readonly IAssignmentRepository _assignments = Substitute.For<IAssignmentRepository>();
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IWorkoutRepository _workouts = Substitute.For<IWorkoutRepository>();
    private readonly IExerciseRepository _exercises = Substitute.For<IExerciseRepository>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(TrainingTestData.Now, TimeSpan.Zero));

    [Fact]
    public async Task An_unknown_client_is_not_found()
    {
        _clients.GetAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((ClientProfile?)null);

        Result<AssignmentResponse> result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Client.NotFound");
    }

    [Fact]
    public async Task An_archived_client_cannot_be_scheduled()
    {
        _clients.GetAsync(TrainingTestData.ClientId, Arg.Any<CancellationToken>())
            .Returns(TrainingTestData.Client(status: ClientStatus.Archived));

        Result<AssignmentResponse> result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value.ShouldBeOfType<DomainError>().Code.ShouldBe("Client.Archived");
    }

    [Fact]
    public async Task An_unknown_workout_is_not_found()
    {
        _clients.GetAsync(TrainingTestData.ClientId, Arg.Any<CancellationToken>()).Returns(TrainingTestData.Client());
        _workouts.GetAsync(Arg.Any<Guid>(), TrainingTestData.TrainerId, Arg.Any<CancellationToken>()).Returns((Workout?)null);

        Result<AssignmentResponse> result = await Handler().Handle(Command(), CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Workout.NotFound");
    }

    [Fact]
    public async Task An_archived_workout_cannot_be_scheduled()
    {
        Workout workout = TrainingTestData.Workout();
        workout.Archive(TrainingTestData.Now);
        _clients.GetAsync(TrainingTestData.ClientId, Arg.Any<CancellationToken>()).Returns(TrainingTestData.Client());
        _workouts.GetAsync(workout.Id, TrainingTestData.TrainerId, Arg.Any<CancellationToken>()).Returns(workout);

        Result<AssignmentResponse> result = await Handler().Handle(Command(workout.Id), CancellationToken.None);

        result.Value.ShouldBeOfType<DomainError>().Code.ShouldBe("Workout.Archived");
    }

    [Fact]
    public async Task Scheduling_persists_the_assignment_with_a_snapshot()
    {
        ClientProfile client = TrainingTestData.Client();
        Workout workout = TrainingTestData.Workout(name: "Upper A");
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _workouts.GetAsync(workout.Id, TrainingTestData.TrainerId, Arg.Any<CancellationToken>()).Returns(workout);

        Result<AssignmentResponse> result = await Handler().Handle(Command(workout.Id), CancellationToken.None);

        AssignmentResponse response = result.Value.ShouldBeOfType<Success<AssignmentResponse>>().Value;
        response.ClientId.ShouldBe(client.Id);
        response.WorkoutId.ShouldBe(workout.Id);
        response.Workout.Name.ShouldBe("Upper A");
        response.Status.ShouldBe(AssignmentStatus.Scheduled);
        response.Date.ShouldBe(TrainingTestData.Today);
        await _assignments.Received(1).AddAsync(
            Arg.Is<Assignment>(a => a.ClientId == client.Id && a.TrainerId == TrainingTestData.TrainerId && a.Workout.Name == "Upper A"),
            Arg.Any<CancellationToken>());
    }

    private static CreateAssignmentCommand Command(Guid? workoutId = null) =>
        new(TrainingTestData.TrainerId, TrainingTestData.ClientId, workoutId ?? Guid.NewGuid(), TrainingTestData.Today, null);

    private CreateAssignmentHandler Handler() => new(_assignments, _clients, _workouts, _exercises, _time);
}
