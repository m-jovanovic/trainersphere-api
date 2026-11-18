using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// QA-F11: a workout line that references an unknown, foreign or archived exercise is a domain problem (404
/// Exercise.NotFound, 422 Exercise.Archived) with per-item errors keyed by the line, not a generic 400 Validation.
/// A foreign exercise looks exactly like an unknown one, so existence never leaks across trainers.
/// </summary>
public sealed class WorkoutExerciseValidationTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task UnknownExercise_Is404WithPerItemErrors()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await PostWorkoutAsync(trainer, Guid.NewGuid());

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
        problem.GetProperty("errors").GetProperty("exercises[0].exerciseId").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ForeignExercise_LooksExactlyLikeUnknown()
    {
        TestUser owner = await CreateTrainerAsync();
        TestUser trainer = await CreateTrainerAsync();
        Guid foreign = await CreateExerciseAsync(owner, "Owner Only Curl");

        HttpResponseMessage response = await PostWorkoutAsync(trainer, foreign);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
        problem.GetProperty("errors").GetProperty("exercises[0].exerciseId").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task ArchivedExercise_Is422WithPerItemErrors()
    {
        TestUser trainer = await CreateTrainerAsync();
        Guid archived = await CreateExerciseAsync(trainer, "Retired Press");
        await (await trainer.Client.DeleteAsync($"/api/exercises/{archived}", Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await PostWorkoutAsync(trainer, archived);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Exercise.Archived");
        problem.GetProperty("errors").GetProperty("exercises[0].exerciseId").EnumerateArray().ShouldNotBeEmpty();
    }

    private static async Task<HttpResponseMessage> PostWorkoutAsync(TestUser trainer, Guid exerciseId) =>
        await trainer.Client.PostAsJsonAsync(
            "/api/workouts",
            new
            {
                name = "Test Workout",
                description = (string?)null,
                exercises = new[]
                {
                    new
                    {
                        exerciseId,
                        sets = 3,
                        goal = new { type = "reps", min = 8, max = 12 },
                        load = new { type = "weight", value = 20m },
                        restSeconds = 90,
                    },
                },
            },
            Ct);

    private static async Task<Guid> CreateExerciseAsync(TestUser trainer, string name)
    {
        JsonElement taxonomy = await (await trainer.Client.GetAsync("/api/exercises/taxonomy", Ct)).ReadJsonAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                name,
                category = taxonomy.GetProperty("categories").EnumerateArray().First().GetString(),
                movementPattern = taxonomy.GetProperty("movementPatterns").EnumerateArray().First().GetString(),
                primaryMuscles = new[] { taxonomy.GetProperty("muscles").EnumerateArray().First().GetString() },
                secondaryMuscles = Array.Empty<string>(),
                equipment = Array.Empty<string>(),
                unilateral = false,
            },
            Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return (await response.ReadJsonAsync()).GetProperty("id").GetGuid();
    }
}
