using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Every "one of" validation message uses the contract form "Must be one of: a, b." with no property-name prefix,
/// whether the value arrives in a body, a query enum filter or a sort parameter. No other message names its field
/// either: the field is the key it is listed under.
/// </summary>
public sealed class OneOfMessageTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task WorkoutGoalType_UsesTheContractMessage()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await PostWorkoutAsync(trainer, goalType: "sets", loadType: "weight");

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        Messages(problem, "exercises[0].goal.type").ShouldBe(["Must be one of: reps, time, distance."]);
    }

    [Fact]
    public async Task WorkoutLoadType_UsesTheContractMessage()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await PostWorkoutAsync(trainer, goalType: "reps", loadType: "kg");

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        string message = Messages(problem, "exercises[0].load.type").ShouldHaveSingleItem();
        message.ShouldStartWith("Must be one of: ");
        message.ShouldContain("percent-1rm");
    }

    [Theory]
    [InlineData("/api/workouts?sort=bogus", "sort")]
    [InlineData("/api/clients?sort=bogus", "sort")]
    [InlineData("/api/clients?status=bogus", "status")]
    [InlineData("/api/invitations?status=bogus", "status")]
    public async Task QueryParameters_UseTheContractMessage(string url, string field)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(url, Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        string message = Messages(problem, field).ShouldHaveSingleItem();
        message.ShouldStartWith("Must be one of: ");
        message.ShouldEndWith(".");
    }

    [Fact]
    public async Task BuiltInRules_DoNotNameTheField()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync("/api/workouts?page=0&pageSize=500", Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        Messages(problem, "page").ShouldBe(["Must be at least 1."]);
        Messages(problem, "pageSize").ShouldBe(["Must be between 1 and 100."]);
    }

    [Fact]
    public async Task CustomRules_DoNotNameTheField()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            "/api/workouts",
            new
            {
                name = " ",
                exercises = new[]
                {
                    new
                    {
                        exerciseId = Guid.NewGuid(),
                        sets = 3,
                        goal = new { type = "reps", min = 0, max = 12 },
                        load = new { type = "weight", value = 20m },
                        restSeconds = 90,
                        tempo = "slow",
                        supersetGroup = "ab",
                    },
                },
            },
            Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        Messages(problem, "name").ShouldBe(["Must not be empty."]);
        Messages(problem, "exercises[0].goal.min").ShouldBe(["Must be between 1 and 100."]);
        Messages(problem, "exercises[0].tempo").ShouldBe(["Must look like 3-1-X-0 (four digits or X separated by dashes)."]);
        Messages(problem, "exercises[0].supersetGroup").ShouldBe(["Must be a single capital letter."]);
    }

    private static string[] Messages(JsonElement problem, string field) =>
        [.. problem.GetProperty("errors").GetProperty(field).EnumerateArray().Select(e => e.GetString()!)];

    private static Task<HttpResponseMessage> PostWorkoutAsync(TestUser trainer, string goalType, string loadType) =>
        trainer.Client.PostAsJsonAsync(
            "/api/workouts",
            new
            {
                name = "Message Check",
                description = (string?)null,
                exercises = new[]
                {
                    new
                    {
                        exerciseId = Guid.NewGuid(),
                        sets = 3,
                        goal = new { type = goalType, min = 8, max = 12 },
                        load = new { type = loadType, value = 20m },
                        restSeconds = 90,
                    },
                },
            },
            Ct);
}
