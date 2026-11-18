using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 7: workout templates - CRUD, prescription validation, superset rules, exercise references, archiving.
/// </summary>
public sealed class WorkoutTemplatesTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] WorkoutProperties = ["id", "name", "description", "archived", "createdAt", "updatedAt", "exercises"];

    private static readonly string[] LineProperties =
        ["id", "position", "exerciseId", "exercise", "sets", "goal", "load", "restSeconds", "tempo", "supersetGroup", "notes"];

    [Fact]
    public async Task CreateWorkout_Returns201WithPositionsAndMicrosecondTimestamps()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        Exercise hinge = await LibraryExerciseAsync(trainer, "barbell-deadlift");

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Workouts,
            new WorkoutBuilder()
                .WithName(Unique.Name("Lower A"))
                .WithDescription("Squat focus.")
                .WithExercises(
                    new WorkoutExerciseBuilder(squat.Id).WithSets(4).WithGoal("reps", 6, 8).WithLoad("weight", 80m).WithRestSeconds(150).WithTempo("3-1-1-0").WithNotes("Belt on top sets.").Build(),
                    new WorkoutExerciseBuilder(hinge.Id).WithSets(3).WithGoal("time", 30, 30).WithLoad("bodyweight").WithRestSeconds(60).WithSupersetGroup("A").Build(),
                    new WorkoutExerciseBuilder(squat.Id).WithSupersetGroup("A").Build())
                .Build(),
            HttpAssertions.Json,
            Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.ShouldHaveExactProperties(WorkoutProperties);
        body.ShouldHaveAtMostMicroseconds("createdAt");
        body.ShouldHaveAtMostMicroseconds("updatedAt");
        body.GetProperty("createdAt").GetString().ShouldBe(body.GetProperty("updatedAt").GetString());
        body.GetProperty("archived").GetBoolean().ShouldBeFalse();

        JsonElement[] lines = [.. body.GetProperty("exercises").EnumerateArray()];
        lines.Length.ShouldBe(3);
        foreach (JsonElement line in lines)
        {
            line.ShouldHaveExactProperties(LineProperties);
            line.GetProperty("exercise").ShouldHaveExactProperties("id", "slug", "name", "illustration");
            line.GetProperty("goal").ShouldHaveExactProperties("type", "min", "max");
            line.GetProperty("load").ShouldHaveExactProperties("type", "value");
        }

        lines[0].GetProperty("position").GetInt32().ShouldBe(1);
        lines[0].GetProperty("exerciseId").GetGuid().ShouldBe(squat.Id);
        lines[0].GetProperty("sets").GetInt32().ShouldBe(4);
        lines[0].GetProperty("goal").GetProperty("type").GetString().ShouldBe("reps");
        lines[0].GetProperty("load").GetProperty("value").GetDecimal().ShouldBe(80m);
        lines[0].GetProperty("tempo").GetString().ShouldBe("3-1-1-0");
        lines[0].ShouldBeNullProperty("supersetGroup");
        lines[1].GetProperty("position").GetInt32().ShouldBe(2);
        lines[1].GetProperty("supersetGroup").GetString().ShouldBe("A");
        lines[1].GetProperty("load").GetProperty("type").GetString().ShouldBe("bodyweight");
        lines[1].ShouldBeNullProperty("tempo");
        lines[2].GetProperty("position").GetInt32().ShouldBe(3);
        lines[2].GetProperty("supersetGroup").GetString().ShouldBe("A");

        // The write response is the same document a later read returns (QA-F4).
        HttpResponseMessage rereadResponse = await (await trainer.Client.GetAsync(TrainingRoutes.Workout(body.GetProperty("id").GetGuid()), Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        string reread = await rereadResponse.Content.ReadAsStringAsync(Ct);
        reread.ShouldContain(body.GetProperty("createdAt").GetString()!);
        reread.ShouldContain(body.GetProperty("updatedAt").GetString()!);
    }

    [Fact]
    public async Task CreateWorkout_IgnoresInputLineIdsPositionsAndExerciseObjects()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        string body = $$"""
            { "name": "{{Unique.Name("Workout")}}", "description": null, "exercises": [
              { "id": "{{Guid.NewGuid()}}", "position": 9, "exercise": { "id": "{{Guid.NewGuid()}}" }, "exerciseId": "{{squat.Id}}",
                "sets": 2, "goal": { "type": "reps", "min": 5, "max": 5 }, "load": { "type": "none", "value": null }, "restSeconds": 0 }
            ] }
            """;

        HttpResponseMessage response = await trainer.Client.PostAsync(
            TrainingRoutes.Workouts, new StringContent(body, Encoding.UTF8, "application/json"), Ct);

        JsonElement created = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        JsonElement line = created.GetProperty("exercises").EnumerateArray().Single();
        line.GetProperty("position").GetInt32().ShouldBe(1);
        line.GetProperty("exercise").GetProperty("slug").GetString().ShouldBe("barbell-back-squat");
    }

    [Theory]
    [InlineData("REPS")]
    [InlineData("rep")]
    [InlineData("percent-1rm")]
    [InlineData("0")]
    public async Task CreateWorkout_WithANonContractGoalType_Returns400OnThatType(string type)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(squat.Id, goal: $$"""{ "type": "{{type}}", "min": 8, "max": 12 }"""));

        await response.ShouldBeValidationProblemAsync("exercises[0].goal.type");
    }

    [Theory]
    [InlineData("kilos")]
    [InlineData("WEIGHT")]
    [InlineData("body-weight")]
    public async Task CreateWorkout_WithANonContractLoadType_Returns400OnThatType(string type)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(squat.Id, load: $$"""{ "type": "{{type}}", "value": 60 }"""));

        await response.ShouldBeValidationProblemAsync("exercises[0].load.type");
    }

    [Fact(Skip = "QA-F9: enum errors read ''Type' must be one of: ...', not the contract's 'Must be one of: ...'")]
    public async Task CreateWorkout_WithANonContractGoalType_ReturnsTheContractMessage()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(squat.Id, goal: """{ "type": "REPS", "min": 8, "max": 12 }"""));

        Problem problem = await response.ShouldBeValidationProblemAsync("exercises[0].goal.type");
        problem.Errors!["exercises[0].goal.type"][0].ShouldStartWith("Must be one of: reps, time, distance.");
    }

    [Fact(Skip = "QA-F10: the API rejects the contract's load type 'percent-1rm'; KebabCase maps Percent1Rm to 'percent1-rm'")]
    public async Task CreateWorkout_WithAPercent1RmLoad_Returns201()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(squat.Id, load: """{ "type": "percent-1rm", "value": 75 }"""));

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.GetProperty("exercises")[0].GetProperty("load").GetProperty("type").GetString().ShouldBe("percent-1rm");
    }

    [Fact(Skip = "QA-F10: the OpenAPI schema documents 'percent1-rm', not the contract's 'percent-1rm'")]
    public async Task OpenApi_DocumentsPercent1RmAsTheLoadType()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        string openApi = await (await trainer.Client.GetAsync("/openapi/v1.json", Ct)).Content.ReadAsStringAsync(Ct);

        openApi.ShouldContain("percent-1rm");
        openApi.ShouldNotContain("percent1-rm");
    }

    [Theory]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 0, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].sets")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 21, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].sets")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 0, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].goal.min")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 5 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].goal.max")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 101 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].goal.max")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "time", "min": 7201, "max": 7201 }, "load": { "type": "none", "value": null }, "restSeconds": 90 }"""", "exercises[0].goal.min")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "distance", "min": 100001, "max": 100001 }, "load": { "type": "none", "value": null }, "restSeconds": 90 }"""", "exercises[0].goal.min")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": -1 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 1000.01 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60.123 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "rpe", "value": 0.7 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "rpe", "value": 11 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "none", "value": 10 }, "restSeconds": 90 }"""", "exercises[0].load.value")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": -1 }"""", "exercises[0].restSeconds")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 1801 }"""", "exercises[0].restSeconds")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90, "tempo": "3-1" }"""", "exercises[0].tempo")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90, "tempo": "3-1-x-0" }"""", "exercises[0].tempo")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90, "supersetGroup": "a" }"""", "exercises[0].supersetGroup")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": { "type": "weight", "value": 60 }, "restSeconds": 90, "supersetGroup": "AB" }"""", "exercises[0].supersetGroup")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": null, "load": { "type": "weight", "value": 60 }, "restSeconds": 90 }"""", "exercises[0].goal")]
    [InlineData(""""{ "exerciseId": "$ID$", "sets": 3, "goal": { "type": "reps", "min": 8, "max": 12 }, "load": null, "restSeconds": 90 }"""", "exercises[0].load")]
    public async Task CreateWorkout_WithAnInvalidPrescription_Returns400OnThatField(string line, string field)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, line.Replace("$ID$", squat.Id.ToString(), StringComparison.Ordinal));

        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.Errors.ShouldNotBeNull();
        problem.Errors.Keys.ShouldContain(k => k == field || k.StartsWith(field + "[", StringComparison.Ordinal));
    }

    [Fact]
    public async Task CreateWorkout_WithoutExercises_Returns400OnExercises()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await PostWorkoutAsync(
            trainer, null, $$"""{ "name": "{{Unique.Name("Workout")}}", "description": null, "exercises": [] }""");

        await response.ShouldBeValidationProblemAsync("exercises");
    }

    [Fact]
    public async Task CreateWorkout_With31Exercises_Returns400OnExercises()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        string lines = string.Join(", ", Enumerable.Range(0, 31).Select(_ => LineJson(squat.Id)));

        HttpResponseMessage response = await PostWorkoutAsync(trainer, null, Wrap(Unique.Name("Workout"), lines));

        await response.ShouldBeValidationProblemAsync("exercises");
    }

    [Theory]
    [InlineData("")]
    [InlineData("xxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxxx")]
    public async Task CreateWorkout_WithAnInvalidName_Returns400OnName(string nameSuffix)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, null, Wrap(nameSuffix, LineJson(squat.Id)));

        await response.ShouldBeValidationProblemAsync("name");
    }

    [Fact]
    public async Task CreateWorkout_WithOneSupersetLetterOnASingleLine_Returns400()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Workouts,
            new WorkoutBuilder().WithExercise(new WorkoutExerciseBuilder(squat.Id).WithSupersetGroup("A").Build()).Build(),
            HttpAssertions.Json,
            Ct);

        await response.ShouldBeValidationProblemAsync("exercises[0].supersetGroup");
    }

    [Fact]
    public async Task CreateWorkout_WithANonConsecutiveSupersetLetter_Returns400()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        Exercise hinge = await LibraryExerciseAsync(trainer, "barbell-deadlift");

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Workouts,
            new WorkoutBuilder().WithExercises(
                new WorkoutExerciseBuilder(squat.Id).WithSupersetGroup("A").Build(),
                new WorkoutExerciseBuilder(hinge.Id).Build(),
                new WorkoutExerciseBuilder(squat.Id).WithSupersetGroup("A").Build()).Build(),
            HttpAssertions.Json,
            Ct);

        await response.ShouldBeValidationProblemAsync("exercises[0].supersetGroup", "exercises[2].supersetGroup");
    }

    [Fact]
    public async Task CreateWorkout_WithAConsecutiveSuperset_Returns201()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        Exercise hinge = await LibraryExerciseAsync(trainer, "barbell-deadlift");

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Workouts,
            new WorkoutBuilder().WithExercises(
                new WorkoutExerciseBuilder(squat.Id).WithSupersetGroup("A").Build(),
                new WorkoutExerciseBuilder(hinge.Id).WithSupersetGroup("A").Build()).Build(),
            HttpAssertions.Json,
            Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact(Skip = "QA-F11: a workout referencing an unknown exercise returns 400 Validation, not the contract's 404 Exercise.NotFound")]
    public async Task CreateWorkout_WithAnUnknownExercise_Returns404ExerciseNotFound()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(Guid.NewGuid()));

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
    }

    [Fact(Skip = "QA-F11: a workout referencing an archived exercise returns 400 Validation, not the contract's 422 Exercise.Archived")]
    public async Task CreateWorkout_WithAnArchivedExercise_Returns422ExerciseArchived()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise archived = await CreateCustomExerciseAsync(trainer);
        await (await trainer.Client.DeleteAsync(TrainingRoutes.Exercise(archived.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(archived.Id));

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Exercise.Archived");
    }

    [Fact(Skip = "QA-F11: a workout referencing another trainer's exercise returns 400 Validation, not the contract's 404 Exercise.NotFound")]
    public async Task CreateWorkout_WithAnotherTrainersExercise_Returns404ExerciseNotFound()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser other = await CreateUtcTrainerAsync();
        Exercise foreign = await CreateCustomExerciseAsync(other);

        HttpResponseMessage response = await PostWorkoutAsync(trainer, LineJson(foreign.Id));

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
    }

    [Fact]
    public async Task UpdateWorkout_FullReplace_ReordersAndReplacesEverything()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        Exercise hinge = await LibraryExerciseAsync(trainer, "barbell-deadlift");
        WorkoutItem workout = await CreateWorkoutAsync(trainer, b => b.WithExercise(new WorkoutExerciseBuilder(squat.Id).Build()));
        await Task.Delay(1100, Ct);

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Workout(workout.Id),
            new WorkoutBuilder()
                .WithName(Unique.Name("Renamed"))
                .WithDescription(null)
                .WithExercises(
                    new WorkoutExerciseBuilder(hinge.Id).WithSets(5).Build(),
                    new WorkoutExerciseBuilder(squat.Id).WithSets(1).Build())
                .Build(),
            HttpAssertions.Json,
            Ct);

        WorkoutItem updated = await response.ShouldHaveJsonAsync<WorkoutItem>();
        updated.Exercises.Select(e => e.ExerciseId).ShouldBe([hinge.Id, squat.Id]);
        updated.Exercises.Select(e => e.Position).ShouldBe([1, 2]);
        updated.Exercises[0].Sets.ShouldBe(5);
        updated.UpdatedAt.ShouldBeGreaterThan(workout.UpdatedAt);
        updated.CreatedAt.ShouldBe(workout.CreatedAt);

        WorkoutItem reread = await (await trainer.Client.GetAsync(TrainingRoutes.Workout(workout.Id), Ct)).ShouldHaveJsonAsync<WorkoutItem>();
        reread.ShouldBeJson(updated);
    }

    [Fact]
    public async Task UpdateWorkout_OmittingExercises_Returns400AndChangesNothing()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PutAsync(
            TrainingRoutes.Workout(workout.Id),
            new StringContent("""{ "name": "Whatever", "description": null }""", Encoding.UTF8, "application/json"),
            Ct);

        await response.ShouldBeValidationProblemAsync("exercises");
        WorkoutItem reread = await (await trainer.Client.GetAsync(TrainingRoutes.Workout(workout.Id), Ct)).ShouldHaveJsonAsync<WorkoutItem>();
        reread.ShouldBeJson(workout);
    }

    [Fact]
    public async Task UpdateWorkout_KeepsAnArchivedExerciseItAlreadyUses()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise custom = await CreateCustomExerciseAsync(trainer);
        WorkoutItem workout = await CreateWorkoutAsync(trainer, b => b.WithExercise(new WorkoutExerciseBuilder(custom.Id).Build()));
        await (await trainer.Client.DeleteAsync(TrainingRoutes.Exercise(custom.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Workout(workout.Id),
            new WorkoutBuilder().WithName(Unique.Name("Still fine")).WithExercise(new WorkoutExerciseBuilder(custom.Id).Build()).Build(),
            HttpAssertions.Json,
            Ct);

        WorkoutItem updated = await response.ShouldHaveJsonAsync<WorkoutItem>();
        updated.Exercises.Single().ExerciseId.ShouldBe(custom.Id);
    }

    [Fact]
    public async Task UpdateWorkout_ForAnotherTrainer_Returns404()
    {
        TestUser owner = await CreateUtcTrainerAsync();
        TestUser other = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(owner);
        Exercise squat = await LibraryExerciseAsync(other, "barbell-back-squat");

        HttpResponseMessage response = await other.Client.PutAsJsonAsync(
            TrainingRoutes.Workout(workout.Id),
            new WorkoutBuilder().WithExercise(new WorkoutExerciseBuilder(squat.Id).Build()).Build(),
            HttpAssertions.Json,
            Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Workout.NotFound");
    }

    [Fact]
    public async Task GetWorkout_ForAClient_Returns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await client.Client.GetAsync(TrainingRoutes.Workout(workout.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Workout.NotFound");
    }

    [Fact]
    public async Task GetWorkout_WithAnUnknownId_Returns404()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(TrainingRoutes.Workout(Guid.NewGuid()), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Workout.NotFound");
    }

    [Fact]
    public async Task DuplicateWorkout_ReturnsACopyWithNewIds()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        WorkoutItem workout = await CreateWorkoutAsync(trainer, b => b
            .WithName("Lower A")
            .WithExercises(
                new WorkoutExerciseBuilder(squat.Id).WithSets(4).Build(),
                new WorkoutExerciseBuilder(squat.Id).WithSets(2).Build()));

        WorkoutItem copy = await (await trainer.Client.PostAsync(TrainingRoutes.WorkoutDuplicate(workout.Id), null, Ct))
            .ShouldHaveJsonAsync<WorkoutItem>(HttpStatusCode.Created);

        copy.Id.ShouldNotBe(workout.Id);
        copy.Name.ShouldBe("Lower A (copy)");
        copy.Archived.ShouldBeFalse();
        copy.Exercises.Select(e => e.Sets).ShouldBe([4, 2]);
        copy.Exercises.Select(e => e.Id).ShouldNotContain(workout.Exercises.Select(e => e.Id).First());
    }

    [Fact]
    public async Task DeleteWorkout_ArchivesItButKeepsExistingAssignmentSnapshots()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.Workout(workout.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        Paged<WorkoutSummary> list = await (await trainer.Client.GetAsync(TrainingRoutes.Workouts, Ct)).ShouldHaveJsonAsync<Paged<WorkoutSummary>>();
        list.Items.ShouldNotContain(w => w.Id == workout.Id);

        Paged<WorkoutSummary> withArchived = await (await trainer.Client.GetAsync($"{TrainingRoutes.Workouts}?includeArchived=true", Ct))
            .ShouldHaveJsonAsync<Paged<WorkoutSummary>>();
        withArchived.Items.ShouldContain(w => w.Id == workout.Id && w.Archived);

        JsonElement reread = await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(assignment.Id), Ct)).ReadJsonAsync();
        reread.GetProperty("workout").GetProperty("name").GetString().ShouldBe(workout.Name);
        reread.GetProperty("workout").GetProperty("exercises").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ListWorkouts_SummariesCarryExerciseCountAndSupportSearchAndSort()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Exercise squat = await LibraryExerciseAsync(trainer, "barbell-back-squat");
        Exercise hinge = await LibraryExerciseAsync(trainer, "barbell-deadlift");
        WorkoutItem alpha = await CreateWorkoutAsync(trainer, b => b.WithName(Unique.Name("Alpha")).WithExercise(new WorkoutExerciseBuilder(squat.Id).Build()));
        WorkoutItem beta = await CreateWorkoutAsync(trainer, b => b
            .WithName(Unique.Name("Beta"))
            .WithExercises(new WorkoutExerciseBuilder(squat.Id).Build(), new WorkoutExerciseBuilder(hinge.Id).Build()));

        Paged<WorkoutSummary> list = await (await trainer.Client.GetAsync($"{TrainingRoutes.Workouts}?sort=-updatedAt", Ct))
            .ShouldHaveJsonAsync<Paged<WorkoutSummary>>();

        list.Items.Count.ShouldBe(2);
        list.Items[0].Id.ShouldBe(beta.Id);
        WorkoutSummary summary = list.Items.Single(w => w.Id == beta.Id);
        summary.ExerciseCount.ShouldBe(2);
        summary.Archived.ShouldBeFalse();

        string needle = alpha.Name[..8];
        Paged<WorkoutSummary> searched = await (await trainer.Client.GetAsync($"{TrainingRoutes.Workouts}?search={needle}", Ct))
            .ShouldHaveJsonAsync<Paged<WorkoutSummary>>();
        searched.Items.ShouldContain(w => w.Id == alpha.Id);
        searched.Items.ShouldNotContain(w => w.Id == beta.Id);
    }

    [Fact]
    public async Task ListWorkouts_WithAnUnknownSort_Returns400OnSort()
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync($"{TrainingRoutes.Workouts}?sort=bananas", Ct);

        await response.ShouldBeValidationProblemAsync("sort");
    }

    [Fact]
    public async Task ListWorkouts_Anonymous_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(TrainingRoutes.Workouts, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    private static string LineJson(Guid exerciseId, string? goal = null, string? load = null) =>
        $$"""{ "exerciseId": "{{exerciseId}}", "sets": 3, "goal": {{goal ?? """{ "type": "reps", "min": 8, "max": 12 }"""}}, "load": {{load ?? """{ "type": "weight", "value": 60 }"""}}, "restSeconds": 90 }""";

    private static string Wrap(string name, string lines) => $$"""{ "name": "{{name}}", "description": null, "exercises": [{{lines}}] }""";

    private static Task<HttpResponseMessage> PostWorkoutAsync(TestUser trainer, string? line, string? body = null)
    {
        string json = body ?? Wrap(Unique.Name("Workout"), line ?? throw new ArgumentNullException(nameof(line)));
        return trainer.Client.PostAsync(TrainingRoutes.Workouts, new StringContent(json, Encoding.UTF8, "application/json"), Ct);
    }
}
