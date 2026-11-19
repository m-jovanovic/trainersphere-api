using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 11: personal records - the baseline rule, the four record types and their formulas, best-set-per-type,
/// current bests, history, and access.
/// </summary>
public sealed class PersonalRecordsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] RecordProperties =
        ["id", "clientId", "exerciseId", "exercise", "type", "value", "previousValue", "weightKg", "reps", "sessionId", "achievedAt"];

    [Fact]
    public async Task FirstSession_SetsTheBaseline_AndReportsNoRecords()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");

        JsonElement session = await CompleteAdHocSessionAsync(client, Set(squat.Id, 100m, 5));

        session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldBeEmpty();
        (await RecordsAsync(client, client.Id)).ShouldBeEmpty();
        Paged<PersonalRecordItem> history = await (await client.Client.GetAsync(TrainingRoutes.PersonalRecordHistory(client.Id), Ct))
            .ShouldHaveJsonAsync<Paged<PersonalRecordItem>>();
        history.TotalCount.ShouldBe(0);
    }

    [Fact]
    public async Task BeatingEveryBest_ReportsAllFourTypesWithTheContractFormulas()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8));

        JsonElement session = await CompleteAdHocSessionAsync(client, Set(squat.Id, 70m, 8), Set(squat.Id, 60m, 10));

        Guid sessionId = session.GetProperty("id").GetGuid();
        JsonElement[] reported = [.. session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray()];
        reported.Length.ShouldBe(4);
        foreach (JsonElement record in reported)
        {
            record.ShouldHaveExactProperties(RecordProperties);
            record.ShouldBeUtcTimestamp("achievedAt");
            record.GetProperty("sessionId").GetGuid().ShouldBe(sessionId);
            record.GetProperty("clientId").GetGuid().ShouldBe(client.Id);
            record.GetProperty("exercise").ShouldHaveExactProperties("id", "slug", "name", "illustration");
            record.GetProperty("exercise").GetProperty("slug").GetString().ShouldBe("barbell-back-squat");
        }

        (string Type, decimal Value, decimal PreviousValue, decimal? WeightKg, int? Reps)[] expected =
        [
            ("estimated-1rm", Epley(70m, 8), Epley(60m, 8), 70m, 8),
            ("max-reps", 10m, 8m, 60m, 10),
            ("max-volume", (70m * 8) + (60m * 10), 60m * 8, null, null),
            ("max-weight", 70m, 60m, 70m, 8),
        ];
        PersonalRecordItem[] current = await RecordsAsync(trainer, client.Id);
        current.Select(r => (r.Type, r.Value, r.PreviousValue, r.WeightKg, r.Reps)).OrderBy(r => r.Type, StringComparer.Ordinal).ShouldBe(expected);
        current.ShouldAllBe(r => r.SessionId == sessionId && r.ExerciseId == squat.Id);
        current.First(r => r.Type == "estimated-1rm").Value.ShouldBe(88.67m);

        // personalRecordCount counts exercises with a record, not records.
        Paged<SessionSummaryItem> sessions = await (await client.Client.GetAsync(TrainingRoutes.Sessions, Ct)).ShouldHaveJsonAsync<Paged<SessionSummaryItem>>();
        sessions.Items.Single(s => s.Id == sessionId).PersonalRecordCount.ShouldBe(1);
    }

    [Fact]
    public async Task OneSession_ReportsOnlyItsBestSetPerType()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8));

        JsonElement session = await CompleteAdHocSessionAsync(client, Set(squat.Id, 65m, 8), Set(squat.Id, 70m, 8));

        JsonElement[] reported = [.. session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray()];
        reported.Select(r => r.GetProperty("type").GetString()).ShouldBe(["estimated-1rm", "max-weight", "max-volume"], ignoreOrder: true);
        reported.Single(r => r.GetProperty("type").GetString() == "estimated-1rm").GetProperty("value").GetDecimal().ShouldBe(Epley(70m, 8));
        reported.Single(r => r.GetProperty("type").GetString() == "max-weight").GetProperty("value").GetDecimal().ShouldBe(70m);
    }

    [Fact]
    public async Task MaxReps_ComparesOnlyWithSetsAtTheSameOrAHigherWeight()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 10));

        JsonElement heavier = await CompleteAdHocSessionAsync(client, Set(squat.Id, 80m, 6));
        JsonElement tie = await CompleteAdHocSessionAsync(client, Set(squat.Id, 50m, 10));
        JsonElement more = await CompleteAdHocSessionAsync(client, Set(squat.Id, 50m, 11));

        Types(heavier).ShouldBe(["estimated-1rm", "max-weight"], ignoreOrder: true);
        Types(tie).ShouldBeEmpty();
        JsonElement record = more.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldHaveSingleItem();
        record.GetProperty("type").GetString().ShouldBe("max-reps");
        record.GetProperty("value").GetDecimal().ShouldBe(11m);
        record.GetProperty("previousValue").GetDecimal().ShouldBe(10m);
        record.GetProperty("weightKg").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task BodyweightSets_CountAsZeroKilogramsForMaxReps()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise pushUp = await CreateCustomExerciseAsync(trainer, b => b.WithName(Unique.Name("Push-up")).WithCategory("strength"));
        await CompleteAdHocSessionAsync(client, Set(pushUp.Id, null, 15));

        JsonElement session = await CompleteAdHocSessionAsync(client, Set(pushUp.Id, null, 18));

        JsonElement record = session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldHaveSingleItem();
        record.GetProperty("type").GetString().ShouldBe("max-reps");
        record.GetProperty("value").GetDecimal().ShouldBe(18m);
        record.GetProperty("previousValue").GetDecimal().ShouldBe(15m);
        record.ShouldBeNullProperty("weightKg");
    }

    [Fact]
    public async Task EstimatedOneRepMax_UsesEpleyFor1To12RepsAndTheWeightForOneRep()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 100m, 1));

        JsonElement thirteenReps = await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 13));
        JsonElement single = await CompleteAdHocSessionAsync(client, Set(squat.Id, 105m, 1));
        JsonElement twelveReps = await CompleteAdHocSessionAsync(client, Set(squat.Id, 80m, 12));

        Types(thirteenReps).ShouldNotContain("estimated-1rm");
        Record(single, "estimated-1rm").GetProperty("value").GetDecimal().ShouldBe(105m);
        Record(single, "estimated-1rm").GetProperty("previousValue").GetDecimal().ShouldBe(100m);
        Record(twelveReps, "estimated-1rm").GetProperty("value").GetDecimal().ShouldBe(Epley(80m, 12));
        Record(twelveReps, "estimated-1rm").GetProperty("previousValue").GetDecimal().ShouldBe(105m);
    }

    [Fact]
    public async Task IncompleteSetsAndTies_NeverSetRecords()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8));

        JsonElement session = await CompleteAdHocSessionAsync(client, Set(squat.Id, 100m, 8, completed: false), Set(squat.Id, 60m, 8));

        Types(session).ShouldBeEmpty();
        (await RecordsAsync(client, client.Id)).ShouldBeEmpty();
    }

    [Fact]
    public async Task IncompleteOnlyFirstSession_DoesNotSetTheBaseline()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8, completed: false));

        JsonElement first = await CompleteAdHocSessionAsync(client, Set(squat.Id, 50m, 8));
        JsonElement second = await CompleteAdHocSessionAsync(client, Set(squat.Id, 55m, 8));

        Types(first).ShouldBeEmpty();
        Record(second, "max-weight").GetProperty("previousValue").GetDecimal().ShouldBe(50m);
    }

    [Fact]
    public async Task CurrentBests_AreTheLatestRecordPerTypeAndHistoryIsNewestFirst()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        Exercise goblet = await LibraryExerciseAsync(client, "goblet-squat");
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m, 8), Set(goblet.Id, 20m, 10, position: 2));
        JsonElement second = await CompleteAdHocSessionAsync(client, Set(squat.Id, 70m, 8), Set(goblet.Id, 24m, 10, position: 2));
        JsonElement third = await CompleteAdHocSessionAsync(client, Set(squat.Id, 80m, 8));

        PersonalRecordItem[] current = await RecordsAsync(trainer, client.Id);
        PersonalRecordItem squatBest = current.Single(r => r.ExerciseId == squat.Id && r.Type == "estimated-1rm");
        squatBest.Value.ShouldBe(Epley(80m, 8));
        squatBest.PreviousValue.ShouldBe(Epley(70m, 8));
        squatBest.SessionId.ShouldBe(third.GetProperty("id").GetGuid());
        current.Single(r => r.ExerciseId == goblet.Id && r.Type == "max-weight").SessionId.ShouldBe(second.GetProperty("id").GetGuid());
        current.GroupBy(r => (r.ExerciseId, r.Type)).ShouldAllBe(g => g.Count() == 1);

        Paged<PersonalRecordItem> squatHistory = await (await client.Client.GetAsync($"{TrainingRoutes.PersonalRecordHistory(client.Id)}?exerciseId={squat.Id}", Ct))
            .ShouldHaveJsonAsync<Paged<PersonalRecordItem>>();
        squatHistory.TotalCount.ShouldBe(6);
        squatHistory.Items.ShouldAllBe(r => r.ExerciseId == squat.Id);
        squatHistory.Items.Select(r => r.AchievedAt).ShouldBeInOrder(SortDirection.Descending);
        squatHistory.Items.Take(3).ShouldAllBe(r => r.SessionId == third.GetProperty("id").GetGuid());

        Paged<PersonalRecordItem> page = await (await client.Client.GetAsync($"{TrainingRoutes.PersonalRecordHistory(client.Id)}?page=2&pageSize=4", Ct))
            .ShouldHaveJsonAsync<Paged<PersonalRecordItem>>();
        page.TotalCount.ShouldBe(9);
        page.TotalPages.ShouldBe(3);
        page.Items.Count.ShouldBe(4);
    }

    [Fact]
    public async Task PersonalRecords_OfSomeoneElsesClient_Return404()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (TestUser otherTrainer, TestUser otherClient) = await CreateUtcTrainerWithClientAsync();

        foreach (TestUser viewer in new[] { otherTrainer, otherClient })
        {
            await (await viewer.Client.GetAsync(TrainingRoutes.PersonalRecords(client.Id), Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
            await (await viewer.Client.GetAsync(TrainingRoutes.PersonalRecordHistory(client.Id), Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
        }
    }

    [Theory]
    [InlineData("pageSize=0", "pageSize")]
    [InlineData("pageSize=101", "pageSize")]
    [InlineData("page=0", "page")]
    [InlineData("exerciseId=squat", "exerciseId")]
    public async Task History_WithAnInvalidQuery_Returns400OnThatParameter(string query, string field)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.GetAsync($"{TrainingRoutes.PersonalRecordHistory(client.Id)}?{query}", Ct);

        await response.ShouldBeValidationProblemAsync(field);
    }

    private static decimal Epley(decimal weightKg, int reps) => decimal.Round(weightKg * (1 + (reps / 30m)), 2);

    private static string?[] Types(JsonElement session) =>
        [.. session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().Select(r => r.GetProperty("type").GetString())];

    private static JsonElement Record(JsonElement session, string type) =>
        session.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().Single(r => r.GetProperty("type").GetString() == type);

    private static async Task<PersonalRecordItem[]> RecordsAsync(TestUser viewer, Guid clientId) =>
        await (await viewer.Client.GetAsync(TrainingRoutes.PersonalRecords(clientId), Ct)).ShouldHaveJsonAsync<PersonalRecordItem[]>();
}
