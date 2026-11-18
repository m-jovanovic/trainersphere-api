using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Base for the Training suites (sections 6-14): trainers and clients in the UTC time zone so calendar-date rules
/// (today, missed, adherence windows) are deterministic, plus the common write flows built through the API.
/// </summary>
public abstract class TrainingTest(ApiFactory factory) : IntegrationTest(factory)
{
    /// <summary>
    /// Finds a seeded library exercise by its exact slug.
    /// </summary>
    protected static async Task<Exercise> LibraryExerciseAsync(TestUser user, string slug)
    {
        // search matches names only, so scan the (small) library page for the slug.
        Paged<Exercise> page = await (await user.Client.GetAsync($"{TrainingRoutes.Exercises}?pageSize=200", Ct))
            .ShouldHaveJsonAsync<Paged<Exercise>>();
        return page.Items.Single(e => e.Slug == slug);
    }

    protected static async Task<Exercise> CreateCustomExerciseAsync(TestUser trainer, Action<ExerciseBuilder>? configure = null)
    {
        ExerciseBuilder builder = new();
        configure?.Invoke(builder);

        return await (await trainer.Client.PostAsJsonAsync(TrainingRoutes.Exercises, builder.Build(), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<Exercise>(HttpStatusCode.Created);
    }

    protected static async Task<WorkoutItem> CreateWorkoutAsync(TestUser trainer, Action<WorkoutBuilder>? configure = null)
    {
        WorkoutBuilder builder = new();
        if (configure is null)
        {
            Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
            builder.WithExercise(new WorkoutExerciseBuilder(squat.Id).Build());
        }
        else
        {
            configure.Invoke(builder);
        }

        return await (await trainer.Client.PostAsJsonAsync(TrainingRoutes.Workouts, builder.Build(), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<WorkoutItem>(HttpStatusCode.Created);
    }

    protected static async Task<ProgramItem> CreateProgramAsync(TestUser trainer, Action<ProgramBuilder>? configure = null)
    {
        ProgramBuilder builder = new();
        if (configure is null)
        {
            WorkoutItem workout = await CreateWorkoutAsync(trainer);
            builder.WithSlot(1, 1, workout.Id);
        }
        else
        {
            configure.Invoke(builder);
        }

        return await (await trainer.Client.PostAsJsonAsync(TrainingRoutes.Programs, builder.Build(), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<ProgramItem>(HttpStatusCode.Created);
    }

    protected static async Task<AssignmentItem> CreateAssignmentAsync(TestUser trainer, Guid clientId, Guid workoutId, string? date = null, string? notes = null)
    {
        AssignmentBuilder builder = new(clientId, workoutId);
        if (date is not null)
        {
            builder.On(date);
        }

        builder.WithNotes(notes);

        return await (await trainer.Client.PostAsJsonAsync(TrainingRoutes.Assignments, builder.Build(), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AssignmentItem>(HttpStatusCode.Created);
    }

    /// <summary>
    /// Starts a session from an assignment and returns its raw JSON (the shape assertions live in the tests).
    /// </summary>
    protected static async Task<JsonElement> StartSessionAsync(TestUser client, Guid assignmentId, Guid? id = null)
    {
        object body = id is null ? new { assignmentId } : new { id, assignmentId };
        HttpResponseMessage response = await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, body, HttpAssertions.Json, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadJsonAsync();
    }

    protected static async Task<JsonElement> LogSetAsync(TestUser client, Guid sessionId, Guid setId, Guid exerciseId, Action<SetBuilder>? configure = null)
    {
        SetBuilder builder = new(exerciseId);
        configure?.Invoke(builder);
        HttpResponseMessage response = await client.Client.PutAsJsonAsync(TrainingRoutes.SessionSet(sessionId, setId), builder.Build(), HttpAssertions.Json, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    protected static async Task<JsonElement> CompleteSessionAsync(TestUser client, Guid sessionId, object? body = null)
    {
        HttpResponseMessage response = await client.Client.PostAsJsonAsync(TrainingRoutes.SessionComplete(sessionId), body ?? new { }, HttpAssertions.Json, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    protected static async Task<BodyMetricItem> PutBodyMetricAsync(TestUser user, Guid clientId, string date, Action<BodyMetricBuilder>? configure = null)
    {
        BodyMetricBuilder builder = new();
        configure?.Invoke(builder);

        return await (await user.Client.PutAsJsonAsync(TrainingRoutes.BodyMetric(clientId, date), builder.Build(), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<BodyMetricItem>();
    }

    /// <summary>
    /// A completed session for the client: assignment today, one completed set (defaults 60 kg x 8), completed.
    /// </summary>
    protected static async Task<(Guid SessionId, JsonElement Completed)> CreateCompletedSessionAsync(TestUser trainer, TestUser client, decimal weightKg = 60m, int reps = 8)
    {
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        JsonElement session = await StartSessionAsync(client, assignment.Id);
        Guid sessionId = session.GetProperty("id").GetGuid();
        Guid exerciseId = workout.Exercises[0].ExerciseId;
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId, b => b.WithReps(reps).WithWeightKg(weightKg));
        JsonElement completed = await CompleteSessionAsync(client, sessionId);
        return (sessionId, completed);
    }

    protected async Task<TestUser> CreateUtcTrainerAsync() =>
        await CreateTrainerAsync(b => b.WithTimeZone("UTC"));

    protected async Task<(TestUser Trainer, TestUser Client)> CreateUtcTrainerWithClientAsync()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        return (trainer, client);
    }
}
