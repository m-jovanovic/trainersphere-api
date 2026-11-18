using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 10: sessions - starting (idempotent), set upserts (last write wins), completing (summary, PRs,
/// assignment), discarding, listing.
/// </summary>
public sealed class SessionsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] SessionProperties =
        ["id", "clientId", "assignmentId", "workoutName", "status", "startedAt", "completedAt", "notes", "exercises", "summary"];

    private static readonly string[] SetProperties =
        ["id", "exerciseId", "position", "setNumber", "weightKg", "reps", "durationSeconds", "distanceMeters", "rpe", "completed", "notes", "updatedAt"];

    [Fact]
    public async Task StartSession_FromAnAssignment_Returns201WithThePrescriptionAndNoSets()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, new { assignmentId = assignment.Id }, HttpAssertions.Json, Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.ShouldHaveExactProperties(SessionProperties);
        body.ShouldHaveAtMostMicroseconds("startedAt");
        body.GetProperty("status").GetString().ShouldBe("in-progress");
        body.GetProperty("workoutName").GetString().ShouldBe(workout.Name);
        body.GetProperty("clientId").GetGuid().ShouldBe(client.Id);
        body.GetProperty("assignmentId").GetGuid().ShouldBe(assignment.Id);
        body.ShouldBeNullProperty("completedAt");

        JsonElement exercise = body.GetProperty("exercises").EnumerateArray().Single();
        exercise.ShouldHaveExactProperties("position", "exerciseId", "exercise", "prescription", "lastPerformance", "sets");
        exercise.GetProperty("position").GetInt32().ShouldBe(1);
        exercise.GetProperty("prescription").GetProperty("sets").GetInt32().ShouldBe(workout.Exercises[0].Sets);
        exercise.GetProperty("sets").EnumerateArray().ShouldBeEmpty();
        exercise.ShouldBeNullProperty("lastPerformance");

        JsonElement summary = body.GetProperty("summary");
        summary.ShouldHaveExactProperties("totalVolumeKg", "completedSets", "totalReps", "durationSeconds", "personalRecords");
        summary.GetProperty("totalVolumeKg").GetDecimal().ShouldBe(0m);
        summary.GetProperty("personalRecords").EnumerateArray().ShouldBeEmpty();

        AssignmentItem reread = await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct)).ShouldHaveJsonAsync<AssignmentItem>();
        reread.SessionId.ShouldBe(body.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task StartSession_AdHoc_Returns201WithoutAPrescription()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { workoutName = "Saturday grinder" }, HttpAssertions.Json, Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.GetProperty("workoutName").GetString().ShouldBe("Saturday grinder");
        body.ShouldBeNullProperty("assignmentId");
        body.GetProperty("exercises").EnumerateArray().ShouldBeEmpty();
    }

    [Fact]
    public async Task StartSession_WithoutAssignmentOrName_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, new { }, HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("workoutName");
    }

    [Fact]
    public async Task StartSession_WithTheSameClientId_Returns200WithTheExistingSession()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        var id = Guid.NewGuid();

        JsonElement first = await StartSessionAsync(client, assignment.Id, id);
        HttpResponseMessage replay = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { id, assignmentId = assignment.Id }, HttpAssertions.Json, Ct);

        await replay.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await replay.ReadJsonAsync()).GetProperty("id").GetGuid().ShouldBe(first.GetProperty("id").GetGuid());

        Paged<SessionSummaryItem> list = await (await client.Client.GetAsync(TrainingRoutes.Sessions, Ct))
            .ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        list.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task StartSession_WithAnIdUsedByAnotherClient_Returns409IdInUse()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        var id = Guid.NewGuid();
        await (await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, new { id, workoutName = "Mine" }, HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);

        HttpResponseMessage response = await other.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { id, workoutName = "Theirs" }, HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Session.IdInUse");
    }

    [Fact]
    public async Task StartSession_ForTheSameAssignmentTwice_Returns409WithTheSessionId()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        JsonElement session = await StartSessionAsync(client, assignment.Id);

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { assignmentId = assignment.Id }, HttpAssertions.Json, Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Session.AlreadyStartedForAssignment");
        problem.GetProperty("sessionId").GetGuid().ShouldBe(session.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task StartSession_WithStartedAtTooFarInTheFuture_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        string startedAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O");

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { workoutName = "Future", startedAt }, HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("startedAt");
    }

    [Fact]
    public async Task StartSession_AsATrainer_Returns403()
    {
        (TestUser trainer, _) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { workoutName = "Coach lifts" }, HttpAssertions.Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpsertSet_WithTheSameId_IsIdempotentAndLastWriteWins()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        var setId = Guid.NewGuid();

        HttpResponseMessage created = await client.Client.PutAsJsonAsync(
            TrainingRoutes.SessionSet(sessionId, setId),
            new SetBuilder(squat.Id).WithWeightKg(60m).WithReps(8).Build(),
            HttpAssertions.Json,
            Ct);
        JsonElement set = await created.ReadJsonAsync();
        await created.ShouldHaveStatusAsync(HttpStatusCode.OK);
        set.ShouldHaveExactProperties(SetProperties);
        set.ShouldHaveAtMostMicroseconds("updatedAt");
        set.GetProperty("id").GetGuid().ShouldBe(setId);
        set.GetProperty("weightKg").GetDecimal().ShouldBe(60m);
        set.GetProperty("completed").GetBoolean().ShouldBeTrue();

        await (await client.Client.PutAsJsonAsync(
                TrainingRoutes.SessionSet(sessionId, setId),
                new SetBuilder(squat.Id).WithWeightKg(70m).WithReps(5).WithRpe(9m).Build(),
                HttpAssertions.Json,
                Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        JsonElement session = await (await client.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct)).ReadJsonAsync();
        JsonElement[] sets = [.. session.GetProperty("exercises").EnumerateArray().SelectMany(e => e.GetProperty("sets").EnumerateArray())];
        sets.Length.ShouldBe(1);
        sets[0].GetProperty("weightKg").GetDecimal().ShouldBe(70m);
        sets[0].GetProperty("reps").GetInt32().ShouldBe(5);
        sets[0].GetProperty("rpe").GetDecimal().ShouldBe(9m);
    }

    [Fact]
    public async Task UpsertSet_WithAnIdUsedInAnotherSession_Returns409SetIdInUse()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid firstSession = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        Guid secondSession = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        var setId = Guid.NewGuid();
        await LogSetAsync(client, firstSession, setId, squat.Id);

        HttpResponseMessage response = await client.Client.PutAsJsonAsync(
            TrainingRoutes.SessionSet(secondSession, setId), new SetBuilder(squat.Id).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Set.IdInUse");
    }

    [Fact]
    public async Task UpsertSet_AtANewPosition_AddsAnEntryWithoutAPrescription()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();
        Exercise goblet = await LibraryExerciseAsync(client, "goblet-squat");
        await LogSetAsync(client, sessionId, Guid.NewGuid(), goblet.Id, b => b.WithSetNumber(2), position: 3);

        JsonElement session = await (await client.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct)).ReadJsonAsync();

        JsonElement[] entries = [.. session.GetProperty("exercises").EnumerateArray()];
        entries.Length.ShouldBe(2);
        entries[0].GetProperty("position").GetInt32().ShouldBe(1);
        entries[0].GetProperty("prescription").ValueKind.ShouldBe(JsonValueKind.Object);
        JsonElement extra = entries.Single(e => e.GetProperty("position").GetInt32() == 3);
        extra.ShouldBeNullProperty("prescription");
        extra.GetProperty("sets").EnumerateArray().Single().GetProperty("setNumber").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task UpsertSet_WithAnUnknownExercise_Returns400OnExerciseId()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();

        HttpResponseMessage response = await client.Client.PutAsJsonAsync(
            TrainingRoutes.SessionSet(sessionId, Guid.NewGuid()), new SetBuilder(Guid.NewGuid()).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("exerciseId");
    }

    [Theory]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 0, "setNumber": 1, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "position")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 51, "setNumber": 1, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "position")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 0, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "setNumber")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 51, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "setNumber")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": -1, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "weightKg")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 1000.01, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "weightKg")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60.123, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "weightKg")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60, "reps": -1, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "reps")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60, "reps": 1001, "durationSeconds": null, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "reps")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": 0, "completed": true, "notes": null }"""", "rpe")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": 7.3, "completed": true, "notes": null }"""", "rpe")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": 60, "reps": 8, "durationSeconds": null, "distanceMeters": null, "rpe": 11, "completed": true, "notes": null }"""", "rpe")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": null, "reps": null, "durationSeconds": -1, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "durationSeconds")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": null, "reps": null, "durationSeconds": 86401, "distanceMeters": null, "rpe": null, "completed": true, "notes": null }"""", "durationSeconds")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": null, "reps": null, "durationSeconds": null, "distanceMeters": -1, "rpe": null, "completed": true, "notes": null }"""", "distanceMeters")]
    [InlineData(""""{ "exerciseId": "$ID$", "position": 1, "setNumber": 1, "weightKg": null, "reps": null, "durationSeconds": null, "distanceMeters": 1000001, "rpe": null, "completed": true, "notes": null }"""", "distanceMeters")]
    public async Task UpsertSet_WithAnInvalidValue_Returns400OnThatField(string body, string field)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");

        HttpResponseMessage response = await client.Client.PutAsync(
            TrainingRoutes.SessionSet(sessionId, Guid.NewGuid()),
            new StringContent(body.Replace("$ID$", squat.Id.ToString(), StringComparison.Ordinal), Encoding.UTF8, "application/json"),
            Ct);

        await response.ShouldBeValidationProblemAsync(field);
    }

    [Fact]
    public async Task DeleteSet_Returns204_EvenWhenTheSetDoesNotExist()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        var setId = Guid.NewGuid();
        await LogSetAsync(client, sessionId, setId, squat.Id);

        await (await client.Client.DeleteAsync(TrainingRoutes.SessionSet(sessionId, setId), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await client.Client.DeleteAsync(TrainingRoutes.SessionSet(sessionId, setId), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await client.Client.DeleteAsync(TrainingRoutes.SessionSet(sessionId, Guid.NewGuid()), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        JsonElement session = await (await client.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct)).ReadJsonAsync();
        session.GetProperty("exercises").EnumerateArray().SelectMany(e => e.GetProperty("sets").EnumerateArray()).ShouldBeEmpty();
    }

    [Fact]
    public async Task CompleteSession_ComputesTheSummaryAndCompletesTheAssignment()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        Guid exerciseId = workout.Exercises[0].ExerciseId;
        Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId, b => b.WithWeightKg(60m).WithReps(8));
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId, b => b.WithSetNumber(2).WithWeightKg(50m).WithReps(10));
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId, b => b.WithSetNumber(3).WithWeightKg(100m).WithReps(1).WithCompleted(false));

        JsonElement completed = await CompleteSessionAsync(client, sessionId);

        completed.GetProperty("status").GetString().ShouldBe("completed");
        completed.ShouldHaveAtMostMicroseconds("completedAt");
        JsonElement summary = completed.GetProperty("summary");
        summary.GetProperty("totalVolumeKg").GetDecimal().ShouldBe(980m);
        summary.GetProperty("completedSets").GetInt32().ShouldBe(2);
        summary.GetProperty("totalReps").GetInt32().ShouldBe(18);
        summary.GetProperty("durationSeconds").ValueKind.ShouldBe(JsonValueKind.Number);

        AssignmentItem reread = await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct)).ShouldHaveJsonAsync<AssignmentItem>();
        reread.Status.ShouldBe("completed");
        reread.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CompleteSession_Twice_ReturnsTheUnchangedSessionAndNoDuplicateRecords()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), squat.Id);

        JsonElement first = await CompleteSessionAsync(client, sessionId);
        HttpResponseMessage replay = await client.Client.PostAsJsonAsync(TrainingRoutes.SessionComplete(sessionId), new { }, HttpAssertions.Json, Ct);

        JsonElement second = await replay.ReadJsonAsync();
        await replay.ShouldHaveStatusAsync(HttpStatusCode.OK);
        second.GetProperty("completedAt").GetString().ShouldBe(first.GetProperty("completedAt").GetString());
        second.GetProperty("summary").GetProperty("personalRecords").GetArrayLength()
            .ShouldBe(first.GetProperty("summary").GetProperty("personalRecords").GetArrayLength());

        PersonalRecordItem[] records = await (await client.Client.GetAsync(TrainingRoutes.PersonalRecords(client.Id), Ct))
            .ShouldHaveJsonAsync<PersonalRecordItem[]>();
        records.Count(r => r.SessionId == sessionId).ShouldBe(first.GetProperty("summary").GetProperty("personalRecords").GetArrayLength());
    }

    [Fact]
    public async Task CompleteSession_WithCompletedAtBeforeStartedAt_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        string completedAt = DateTimeOffset.UtcNow.AddHours(-2).ToString("O");

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.SessionComplete(sessionId), new { completedAt }, HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("completedAt");
    }

    [Fact]
    public async Task CompleteSession_WithCompletedAtInTheFuture_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        string completedAt = DateTimeOffset.UtcNow.AddMinutes(10).ToString("O");

        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.SessionComplete(sessionId), new { completedAt }, HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("completedAt");
    }

    [Fact]
    public async Task CompletedSession_RejectsSetChangesAndDiscard()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        var setId = Guid.NewGuid();
        await LogSetAsync(client, sessionId, setId, squat.Id);
        await CompleteSessionAsync(client, sessionId);

        await (await client.Client.PutAsJsonAsync(TrainingRoutes.SessionSet(sessionId, setId), new SetBuilder(squat.Id).Build(), HttpAssertions.Json, Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Session.Completed");
        await (await client.Client.DeleteAsync(TrainingRoutes.SessionSet(sessionId, setId), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Session.Completed");
        await (await client.Client.DeleteAsync(TrainingRoutes.Session(sessionId), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "Session.Completed");
    }

    [Fact]
    public async Task DiscardSession_Returns204AndReleasesTheAssignment()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();

        await (await client.Client.DeleteAsync(TrainingRoutes.Session(sessionId), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await client.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Session.NotFound");
        AssignmentItem reread = await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct)).ShouldHaveJsonAsync<AssignmentItem>();
        reread.Status.ShouldBe("scheduled");
        reread.SessionId.ShouldBeNull();

        await (await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, new { assignmentId = assignment.Id }, HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task UpdateSession_RenamesAndEditsNotes()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();

        HttpResponseMessage response = await client.Client.PutAsJsonAsync(
            TrainingRoutes.Session(sessionId), new SessionUpdate("Easy day", "Felt tired."), HttpAssertions.Json, Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        body.GetProperty("workoutName").GetString().ShouldBe("Easy day");
        body.GetProperty("notes").GetString().ShouldBe("Felt tired.");
    }

    [Fact]
    public async Task GetSession_AsTheTrainer_Returns200_AsAnotherClient404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        Guid sessionId = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();

        await (await trainer.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await other.Client.GetAsync(TrainingRoutes.Session(sessionId), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Session.NotFound");
    }

    [Fact]
    public async Task ListSessions_NewestFirst_WithFiltersAndSummaries()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Guid older = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        await Task.Delay(1100, Ct);
        Guid newer = (await StartAdHocAsync(client)).GetProperty("id").GetGuid();
        await LogSetAsync(client, newer, Guid.NewGuid(), squat.Id);
        await CompleteSessionAsync(client, newer);

        Paged<SessionSummaryItem> all = await (await client.Client.GetAsync(TrainingRoutes.Sessions, Ct))
            .ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        all.TotalCount.ShouldBe(2);
        all.Items.Select(s => s.Id).ShouldBe([newer, older]);
        SessionSummaryItem summary = all.Items[0];
        summary.Status.ShouldBe("completed");
        summary.TotalVolumeKg.ShouldBe(480m);
        summary.CompletedSets.ShouldBe(1);
        summary.PersonalRecordCount.ShouldBe(0);

        Paged<SessionSummaryItem> inProgress = await (await client.Client.GetAsync($"{TrainingRoutes.Sessions}?status=in-progress", Ct))
            .ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        inProgress.Items.Select(s => s.Id).ShouldBe([older]);

        Paged<SessionSummaryItem> forTrainer = await (await trainer.Client.GetAsync($"{TrainingRoutes.Sessions}?clientId={client.Id}", Ct))
            .ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        forTrainer.TotalCount.ShouldBe(2);
    }

    [Fact]
    public async Task ListSessions_WithANonContractStatus_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.GetAsync($"{TrainingRoutes.Sessions}?status=done", Ct);

        await response.ShouldBeValidationProblemAsync("status");
    }

    [Fact]
    public async Task StartSession_AfterAPreviousSession_ShowsTheLastPerformance()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        Guid exerciseId = workout.Exercises[0].ExerciseId;

        AssignmentItem first = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        Guid firstSession = (await StartSessionAsync(client, first.Id)).GetProperty("id").GetGuid();
        await LogSetAsync(client, firstSession, Guid.NewGuid(), exerciseId, b => b.WithWeightKg(60m).WithReps(8).WithRpe(7.5m));
        await CompleteSessionAsync(client, firstSession);

        AssignmentItem second = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        JsonElement session = await StartSessionAsync(client, second.Id);

        JsonElement last = session.GetProperty("exercises").EnumerateArray().Single().GetProperty("lastPerformance");
        last.GetProperty("sessionId").GetGuid().ShouldBe(firstSession);
        last.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        JsonElement set = last.GetProperty("sets").EnumerateArray().Single();
        set.GetProperty("weightKg").GetDecimal().ShouldBe(60m);
        set.GetProperty("reps").GetInt32().ShouldBe(8);
        set.GetProperty("rpe").GetDecimal().ShouldBe(7.5m);
    }

    [Fact]
    public async Task PersonalRecordCount_CountsDistinctExercisesWithARecord()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Exercise goblet = await LibraryExerciseAsync(client, "goblet-squat");
        WorkoutItem workout = await CreateWorkoutAsync(trainer, b => b.WithExercises(
            new WorkoutExerciseBuilder(squat.Id).Build(),
            new WorkoutExerciseBuilder(goblet.Id).Build()));

        async Task<Guid> LogAndComplete(decimal squatKg, decimal? gobletKg)
        {
            AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
            Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();
            await LogSetAsync(client, sessionId, Guid.NewGuid(), squat.Id, b => b.WithWeightKg(squatKg).WithReps(8));
            if (gobletKg is { } kg)
            {
                await LogSetAsync(client, sessionId, Guid.NewGuid(), goblet.Id, b => b.WithWeightKg(kg).WithReps(10), position: 2);
            }

            await CompleteSessionAsync(client, sessionId);
            return sessionId;
        }

        // Baseline: the first time an exercise is logged sets no records.
        Guid baseline = await LogAndComplete(60m, 20m);
        JsonElement baselineSession = await (await client.Client.GetAsync(TrainingRoutes.Session(baseline), Ct)).ReadJsonAsync();
        baselineSession.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldBeEmpty();

        Guid both = await LogAndComplete(70m, 25m);
        JsonElement bothSession = await (await client.Client.GetAsync(TrainingRoutes.Session(both), Ct)).ReadJsonAsync();
        bothSession.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldNotBeEmpty();

        Guid oneExercise = await LogAndComplete(80m, null);
        JsonElement oneSession = await (await client.Client.GetAsync(TrainingRoutes.Session(oneExercise), Ct)).ReadJsonAsync();
        oneSession.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldNotBeEmpty();

        Paged<SessionSummaryItem> list = await (await client.Client.GetAsync(TrainingRoutes.Sessions, Ct))
            .ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        list.Items.Single(s => s.Id == baseline).PersonalRecordCount.ShouldBe(0);
        list.Items.Single(s => s.Id == both).PersonalRecordCount.ShouldBe(2);
        list.Items.Single(s => s.Id == oneExercise).PersonalRecordCount.ShouldBe(1);
    }

    [Fact]
    public async Task PersonalRecord_Types_AreTheContractValues()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        WorkoutItem workout = await CreateWorkoutAsync(trainer, b => b.WithExercise(new WorkoutExerciseBuilder(squat.Id).Build()));

        foreach (decimal kg in new[] { 60m, 80m })
        {
            AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
            Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();
            await LogSetAsync(client, sessionId, Guid.NewGuid(), squat.Id, b => b.WithWeightKg(kg).WithReps(8));
            await CompleteSessionAsync(client, sessionId);
        }

        PersonalRecordItem[] records = await (await client.Client.GetAsync(TrainingRoutes.PersonalRecords(client.Id), Ct))
            .ShouldHaveJsonAsync<PersonalRecordItem[]>();
        records.ShouldNotBeEmpty();
        records.Select(r => r.Type).ShouldAllBe(t => t == "estimated-1rm" || t == "max-weight" || t == "max-reps" || t == "max-volume");
    }

    private static async Task<JsonElement> StartAdHocAsync(TestUser client)
    {
        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { workoutName = Unique.Name("Ad-hoc") }, HttpAssertions.Json, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadJsonAsync();
    }
}
