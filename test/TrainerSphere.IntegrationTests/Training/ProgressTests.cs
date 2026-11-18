using System.Globalization;
using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 11: progress - exercises performed, per-exercise points, and the weekly summary with adherence and
/// streaks.
/// </summary>
public sealed class ProgressTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ProgressExercises_ListsCompletedWorkPerExercise()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Exercise goblet = await LibraryExerciseAsync(client, "goblet-squat");
        Exercise deadlift = await LibraryExerciseAsync(client, "barbell-deadlift");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8), Set(goblet.Id, 20m, 10, position: 2));
        JsonElement latest = await CompleteAdHocSessionAsync(client, Set(squat.Id, 70m, 8), Set(deadlift.Id, 100m, 5, position: 2, completed: false));
        JsonElement inProgress = await StartAdHocAsync(client);
        await LogSetAsync(client, inProgress.GetProperty("id").GetGuid(), Guid.NewGuid(), deadlift.Id);

        JsonElement[] items = [.. (await (await trainer.Client.GetAsync(TrainingRoutes.ProgressExercises(client.Id), Ct)).ReadJsonAsync()).EnumerateArray()];

        items.Length.ShouldBe(2);
        foreach (JsonElement item in items)
        {
            item.ShouldHaveExactProperties("exerciseId", "exercise", "sessionCount", "lastPerformedAt");
            item.GetProperty("exercise").ShouldHaveExactProperties("id", "slug", "name", "illustration");
        }

        JsonElement squatItem = items.Single(i => i.GetProperty("exerciseId").GetGuid() == squat.Id);
        squatItem.GetProperty("sessionCount").GetInt32().ShouldBe(2);
        squatItem.GetProperty("lastPerformedAt").GetDateTimeOffset().ShouldBe(latest.GetProperty("completedAt").GetDateTimeOffset());
        items.Single(i => i.GetProperty("exerciseId").GetGuid() == goblet.Id).GetProperty("sessionCount").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task ExerciseProgress_HasOnePointPerSessionWithTheTopSetAndTotals()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset tenDaysAgo = DateTimeOffset.UtcNow.AddDays(-10);
        JsonElement older = await CompleteAdHocSessionStartedAtAsync(client, tenDaysAgo, Set(squat.Id, 50m, 5));
        JsonElement today = await CompleteAdHocSessionAsync(
            client, Set(squat.Id, 60m, 10), Set(squat.Id, 80m, 8), Set(squat.Id, 80m, 6), Set(squat.Id, 100m, 5, completed: false));

        JsonElement body = await (await client.Client.GetAsync(TrainingRoutes.ProgressExercise(client.Id, squat.Id), Ct)).ReadJsonAsync();

        body.ShouldHaveExactProperties("exercise", "points");
        body.GetProperty("exercise").GetProperty("id").GetGuid().ShouldBe(squat.Id);
        JsonElement[] points = [.. body.GetProperty("points").EnumerateArray()];
        points.Select(p => p.GetProperty("sessionId").GetGuid()).ShouldBe([older.GetProperty("id").GetGuid(), today.GetProperty("id").GetGuid()]);
        points[0].GetProperty("date").GetString().ShouldBe(DateOf(tenDaysAgo));

        JsonElement point = points[1];
        point.ShouldHaveExactProperties("date", "sessionId", "estimatedOneRepMaxKg", "topSetWeightKg", "topSetReps", "volumeKg", "totalReps", "completedSets");
        point.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        point.GetProperty("estimatedOneRepMaxKg").GetDecimal().ShouldBe(101.33m);
        point.GetProperty("topSetWeightKg").GetDecimal().ShouldBe(80m);
        point.GetProperty("topSetReps").GetInt32().ShouldBe(8);
        point.GetProperty("volumeKg").GetDecimal().ShouldBe((60m * 10) + (80m * 8) + (80m * 6));
        point.GetProperty("totalReps").GetInt32().ShouldBe(24);
        point.GetProperty("completedSets").GetInt32().ShouldBe(3);
    }

    [Fact]
    public async Task ExerciseProgress_FromAndTo_AreInclusiveDateFilters()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionStartedAtAsync(client, DateTimeOffset.UtcNow.AddDays(-10), Set(squat.Id, 50m, 5));
        await CompleteAdHocSessionStartedAtAsync(client, DateTimeOffset.UtcNow.AddDays(-5), Set(squat.Id, 55m, 5));
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 5));

        string[] dates = await PointDatesAsync(client, squat.Id, $"?from={TrainingDates.Offset(-5)}&to={TrainingDates.Offset(-5)}");
        string[] fromOnly = await PointDatesAsync(client, squat.Id, $"?from={TrainingDates.Offset(-5)}");
        string[] toOnly = await PointDatesAsync(client, squat.Id, $"?to={TrainingDates.Offset(-6)}");

        dates.ShouldBe([TrainingDates.Offset(-5)]);
        fromOnly.ShouldBe([TrainingDates.Offset(-5), TrainingDates.Today]);
        toOnly.ShouldBe([TrainingDates.Offset(-10)]);
    }

    [Fact]
    public async Task ExerciseProgress_WithToBeforeFrom_Returns400OnTo()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");

        HttpResponseMessage response = await client.Client.GetAsync(
            $"{TrainingRoutes.ProgressExercise(client.Id, squat.Id)}?from={TrainingDates.Today}&to={TrainingDates.Yesterday}", Ct);

        await response.ShouldBeValidationProblemAsync("to");
    }

    [Fact]
    public async Task ExerciseProgress_ForAnUnknownOrForeignExercise_Returns404ExerciseNotFound()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser otherTrainer = await CreateUtcTrainerAsync();
        Exercise foreign = await CreateCustomExerciseAsync(otherTrainer);

        foreach (Guid exerciseId in new[] { Guid.NewGuid(), foreign.Id })
        {
            HttpResponseMessage response = await client.Client.GetAsync(TrainingRoutes.ProgressExercise(client.Id, exerciseId), Ct);
            await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Exercise.NotFound");
        }
    }

    [Fact]
    public async Task Summary_GroupsCompletedSessionsByIsoWeekOldestFirst()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 10), Set(squat.Id, 60m, 8));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-7), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-14), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-28), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-7 * 13), Set(squat.Id, 50m, 10));

        JsonElement summary = await SummaryAsync(client, string.Empty);

        summary.ShouldHaveExactProperties("weeks", "totalSessions", "adherence7d", "adherence28d", "streakWeeks");
        JsonElement[] weeks = [.. summary.GetProperty("weeks").EnumerateArray()];
        weeks.Length.ShouldBe(12);
        weeks[0].ShouldHaveExactProperties("weekStart", "sessions", "volumeKg", "completedSets");
        string monday = Monday(DateOnly.FromDateTime(now.UtcDateTime));
        weeks.Select(w => w.GetProperty("weekStart").GetString()).ShouldBe(
            [.. Enumerable.Range(0, 12).Select(i => DateOnly.ParseExact(monday, "yyyy-MM-dd", CultureInfo.InvariantCulture).AddDays(-7 * (11 - i)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))]);
        weeks.Select(w => w.GetProperty("sessions").GetInt32()).ShouldBe([0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1, 1]);
        weeks[11].GetProperty("volumeKg").GetDecimal().ShouldBe(1080m);
        weeks[11].GetProperty("completedSets").GetInt32().ShouldBe(2);
        summary.GetProperty("totalSessions").GetInt32().ShouldBe(5);
        summary.GetProperty("streakWeeks").GetInt32().ShouldBe(3);

        JsonElement four = await SummaryAsync(client, "?weeks=4");
        four.GetProperty("weeks").GetArrayLength().ShouldBe(4);
    }

    [Fact]
    public async Task Summary_StreakSkipsAnEmptyCurrentWeek()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-7), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-14), Set(squat.Id, 50m, 10));

        JsonElement summary = await SummaryAsync(client, string.Empty);

        summary.GetProperty("streakWeeks").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task Summary_WithNothingLoggedOrDue_HasZerosAndNullRates()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        JsonElement summary = await SummaryAsync(trainer, string.Empty, client.Id);

        summary.GetProperty("totalSessions").GetInt32().ShouldBe(0);
        summary.GetProperty("streakWeeks").GetInt32().ShouldBe(0);
        summary.ShouldBeNullProperty("adherence7d");
        summary.ShouldBeNullProperty("adherence28d");
        summary.GetProperty("weeks").EnumerateArray().ShouldAllBe(w => w.GetProperty("sessions").GetInt32() == 0);
    }

    [Fact]
    public async Task Summary_Adherence_IsRoundedToTwoDecimals()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        await CreateAdherenceHistoryAsync(trainer, client);

        JsonElement summary = await SummaryAsync(client, string.Empty);

        summary.GetProperty("adherence7d").GetRawText().ShouldBe("0.33");
        summary.GetProperty("adherence28d").GetDecimal().ShouldBe(0.5m);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("53")]
    [InlineData("many")]
    public async Task Summary_WithWeeksOutOfRange_Returns400OnWeeks(string weeks)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.GetAsync($"{TrainingRoutes.ProgressSummary(client.Id)}?weeks={weeks}", Ct);

        await response.ShouldBeValidationProblemAsync("weeks");
    }

    [Fact]
    public async Task Progress_OfSomeoneElsesClient_Returns404()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (TestUser otherTrainer, TestUser otherClient) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");

        foreach (TestUser viewer in new[] { otherTrainer, otherClient })
        {
            foreach (string route in new[] { TrainingRoutes.ProgressExercises(client.Id), TrainingRoutes.ProgressExercise(client.Id, squat.Id), TrainingRoutes.ProgressSummary(client.Id) })
            {
                await (await viewer.Client.GetAsync(route, Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
            }
        }
    }

    private static string DateOf(DateTimeOffset value) => value.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Monday(DateOnly date) =>
        date.AddDays(-(((int)date.DayOfWeek + 6) % 7)).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<string[]> PointDatesAsync(TestUser client, Guid exerciseId, string query)
    {
        JsonElement body = await (await client.Client.GetAsync(TrainingRoutes.ProgressExercise(client.Id, exerciseId) + query, Ct)).ReadJsonAsync();
        return [.. body.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("date").GetString()!)];
    }

    private static async Task<JsonElement> SummaryAsync(TestUser viewer, string query, Guid? clientId = null)
    {
        HttpResponseMessage response = await viewer.Client.GetAsync(TrainingRoutes.ProgressSummary(clientId ?? viewer.Id) + query, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> StartAdHocAsync(TestUser client)
    {
        HttpResponseMessage response = await client.Client.PostAsJsonAsync(
            TrainingRoutes.Sessions, new { workoutName = Unique.Name("Ad-hoc") }, HttpAssertions.Json, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        return await response.ReadJsonAsync();
    }
}
