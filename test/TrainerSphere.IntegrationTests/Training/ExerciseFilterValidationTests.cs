using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// QA-F9: taxonomy validation follows the contract conventions. Query filter errors are keyed by the parameter
/// name with one error per invalid value, and every taxonomy message reads "Must be one of: ...".
/// </summary>
public sealed class ExerciseFilterValidationTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task InvalidMuscleFilter_OneErrorPerInvalidValue_KeyedByParameter()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync("/api/exercises?muscle=bogus&muscle=alsobogus", Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        JsonElement[] errors = problem.GetProperty("errors").GetProperty("muscle").EnumerateArray().ToArray();
        errors.Length.ShouldBe(2);
        errors.ShouldAllBe(e => e.GetString()!.StartsWith("Must be one of: ", StringComparison.Ordinal) && e.GetString()!.EndsWith('.'));
    }

    [Fact]
    public async Task InvalidPatternFilter_KeyedByTheQueryParameterName()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync("/api/exercises?pattern=bogus", Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        JsonElement error = problem.GetProperty("errors").GetProperty("pattern").EnumerateArray().ShouldHaveSingleItem();
        error.GetString()!.ShouldStartWith("Must be one of: ");
    }

    [Fact]
    public async Task CreateExercise_InvalidCategory_MessageListsTheAllowedValues()
    {
        TestUser trainer = await CreateTrainerAsync();
        JsonElement taxonomy = await (await trainer.Client.GetAsync("/api/exercises/taxonomy", Ct)).ReadJsonAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            "/api/exercises",
            new
            {
                name = "Test Lift",
                category = "bogus",
                movementPattern = taxonomy.GetProperty("movementPatterns").EnumerateArray().First().GetString(),
                primaryMuscles = new[] { taxonomy.GetProperty("muscles").EnumerateArray().First().GetString() },
                secondaryMuscles = Array.Empty<string>(),
                equipment = Array.Empty<string>(),
                unilateral = false,
            },
            Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        JsonElement error = problem.GetProperty("errors").GetProperty("category").EnumerateArray().ShouldHaveSingleItem();
        error.GetString()!.ShouldStartWith("Must be one of: ");
    }
}
