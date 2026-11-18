using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 9: assignments - scheduling, the default window and filters, derived <c>missed</c>, rescheduling,
/// skipping, deleting.
/// </summary>
public sealed class AssignmentsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] AssignmentProperties =
    [
        "id", "clientId", "client", "date", "status", "workoutId", "workout", "programAssignmentId", "programName",
        "notes", "sessionId", "completedAt", "createdAt",
    ];

    [Fact]
    public async Task CreateAssignment_Returns201WithTheWorkoutSnapshot()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Assignments, new AssignmentBuilder(client.Id, workout.Id).WithNotes("Go by feel.").Build(), HttpAssertions.Json, Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.ShouldHaveExactProperties(AssignmentProperties);
        body.ShouldHaveAtMostMicroseconds("createdAt");
        body.GetProperty("status").GetString().ShouldBe("scheduled");
        body.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        body.GetProperty("client").ShouldHaveExactProperties("id", "firstName", "lastName");
        body.GetProperty("client").GetProperty("id").GetGuid().ShouldBe(client.Id);
        body.ShouldBeNullProperty("programAssignmentId");
        body.ShouldBeNullProperty("programName");
        body.ShouldBeNullProperty("sessionId");
        body.ShouldBeNullProperty("completedAt");
        body.GetProperty("workout").GetProperty("name").GetString().ShouldBe(workout.Name);
        body.GetProperty("workout").GetProperty("exercises").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task CreateAssignment_ForAnArchivedClient_Returns422ClientArchived()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Assignments, new AssignmentBuilder(client.Id, workout.Id).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Client.Archived");
    }

    [Fact]
    public async Task CreateAssignment_ForAnotherTrainersClient_Returns404()
    {
        (TestUser trainer, _) = await CreateUtcTrainerWithClientAsync();
        (_, TestUser foreign) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Assignments, new AssignmentBuilder(foreign.Id, workout.Id).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
    }

    [Fact]
    public async Task CreateAssignment_AsAClient_Returns403()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Assignments, new AssignmentBuilder(client.Id, workout.Id).Build(), HttpAssertions.Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListAssignments_WithoutDates_UsesTodayMinus7ToPlus28()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(-8));
        AssignmentItem insideLow = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(-7));
        AssignmentItem today = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Today);
        AssignmentItem insideHigh = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(28));
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(29));

        AssignmentItem[] list = await (await trainer.Client.GetAsync($"{TrainingRoutes.Assignments}?clientId={client.Id}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();

        list.Select(a => a.Id).ShouldBe([insideLow.Id, today.Id, insideHigh.Id]);
    }

    [Fact]
    public async Task ListAssignments_FromAndToAreInclusiveAndSortedByDate()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem first = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Today);
        AssignmentItem second = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(2));
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(3));

        AssignmentItem[] list = await (await trainer.Client.GetAsync(
                $"{TrainingRoutes.Assignments}?clientId={client.Id}&from={TrainingDates.Today}&to={TrainingDates.Offset(2)}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();

        list.Select(a => a.Id).ShouldBe([first.Id, second.Id]);
    }

    [Fact]
    public async Task ListAssignments_Beyond92Days_Returns400()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(
            $"{TrainingRoutes.Assignments}?clientId={client.Id}&from={TrainingDates.Today}&to={TrainingDates.Offset(93)}", Ct);

        await response.ShouldBeValidationProblemAsync("to");
    }

    [Fact]
    public async Task ListAssignments_DerivesMissedForPastScheduledOnes()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem past = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Yesterday);
        AssignmentItem today = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Today);
        AssignmentItem future = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);

        AssignmentItem[] list = await (await trainer.Client.GetAsync(
                $"{TrainingRoutes.Assignments}?clientId={client.Id}&from={TrainingDates.Yesterday}&to={TrainingDates.Tomorrow}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();

        list.Single(a => a.Id == past.Id).Status.ShouldBe("missed");
        list.Single(a => a.Id == today.Id).Status.ShouldBe("scheduled");
        list.Single(a => a.Id == future.Id).Status.ShouldBe("scheduled");
    }

    [Fact]
    public async Task ListAssignments_AsAClient_ReturnsOnlyTheirOwn()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem own = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        await CreateAssignmentAsync(trainer, other.Id, workout.Id);

        AssignmentItem[] list = await (await client.Client.GetAsync(TrainingRoutes.Assignments, Ct)).ShouldHaveJsonAsync<AssignmentItem[]>();
        list.Select(a => a.Id).ShouldBe([own.Id]);

        HttpResponseMessage foreign = await client.Client.GetAsync($"{TrainingRoutes.Assignments}?clientId={other.Id}", Ct);
        await foreign.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
    }

    [Fact]
    public async Task GetAssignment_ForAnotherClient_Returns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        HttpResponseMessage response = await other.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Assignment.NotFound");
    }

    [Fact]
    public async Task UpdateAssignment_ReschedulesAndEditsNotes()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        AssignmentItem updated = await (await trainer.Client.PutAsJsonAsync(
                TrainingRoutes.Assignment(assignment.Id), new AssignmentUpdate(TrainingDates.Offset(3), "Moved."), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AssignmentItem>();

        updated.Date.ShouldBe(TrainingDates.Offset(3));
        updated.Notes.ShouldBe("Moved.");
        updated.Status.ShouldBe("scheduled");
    }

    [Fact]
    public async Task UpdateAssignment_WhenCompleted_Returns409NotScheduled()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (Guid _, JsonElement _) = await CreateCompletedSessionAsync(trainer, client);
        AssignmentItem assignment = (await (await trainer.Client.GetAsync($"{TrainingRoutes.Assignments}?clientId={client.Id}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>()).Single();

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Assignment(assignment.Id), new AssignmentUpdate(TrainingDates.Tomorrow, null), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Assignment.NotScheduled");
    }

    [Fact]
    public async Task DeleteAssignment_Returns204_ThenGetReturns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.Assignment(assignment.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Assignment.NotFound");
    }

    [Fact]
    public async Task DeleteAssignment_WithASession_Returns409WithTheSessionId()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        JsonElement session = await StartSessionAsync(client, assignment.Id);

        HttpResponseMessage response = await trainer.Client.DeleteAsync(TrainingRoutes.Assignment(assignment.Id), Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Assignment.NotScheduled");
        problem.GetProperty("sessionId").GetGuid().ShouldBe(session.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task SkipAssignment_MarksItSkipped_AndSkippingAgainReturns409()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        AssignmentItem skipped = await (await client.Client.PostAsJsonAsync(
                TrainingRoutes.AssignmentSkip(assignment.Id), new { reason = "Sick." }, HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AssignmentItem>();
        skipped.Status.ShouldBe("skipped");

        await (await trainer.Client.PostAsJsonAsync(TrainingRoutes.AssignmentSkip(assignment.Id), new { }, HttpAssertions.Json, Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Assignment.NotScheduled");
    }

    [Fact]
    public async Task SkipAssignment_ForAnotherTrainersAssignment_Returns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        HttpResponseMessage response = await other.Client.PostAsJsonAsync(
            TrainingRoutes.AssignmentSkip(assignment.Id), new { }, HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Assignment.NotFound");
    }

    [Fact]
    public async Task CreateAssignment_WithAnUnknownWorkout_Returns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Assignments, new AssignmentBuilder(client.Id, Guid.NewGuid()).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Workout.NotFound");
    }

    [Fact]
    public async Task CreateAssignment_WithAMalformedDate_Returns400OnDate()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        string body = $$"""{ "clientId": "{{client.Id}}", "workoutId": "{{workout.Id}}", "date": "next friday", "notes": null }""";

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Assignments, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        await response.ShouldBeValidationProblemAsync("date");
    }
}
