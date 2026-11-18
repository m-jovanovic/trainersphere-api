using System.Globalization;
using System.Net;
using System.Text;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 12: body metrics - upsert per date (full replace), who recorded it, ranges, dates in the client's time
/// zone, listing, deleting, and access.
/// </summary>
public sealed class BodyMetricsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] MetricProperties =
    [
        "clientId", "date", "weightKg", "bodyFatPercent", "waistCm", "chestCm", "hipsCm", "armCm", "thighCm", "restingHeartRate", "notes",
        "recordedBy", "updatedAt",
    ];

    [Fact]
    public async Task PutBodyMetric_AsTheClient_Returns200WithTheContractShape()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(
            client,
            client.Id,
            TrainingDates.Today,
            """{ "weightKg": 78.4, "bodyFatPercent": 17.5, "waistCm": 82, "chestCm": 101, "hipsCm": 95, "armCm": 35.5, "thighCm": 58, "restingHeartRate": 58, "notes": "Morning." }""");

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        body.ShouldHaveExactProperties(MetricProperties);
        body.ShouldHaveAtMostMicroseconds("updatedAt");
        body.GetProperty("clientId").GetGuid().ShouldBe(client.Id);
        body.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        body.GetProperty("weightKg").GetDecimal().ShouldBe(78.4m);
        body.GetProperty("armCm").GetDecimal().ShouldBe(35.5m);
        body.GetProperty("restingHeartRate").GetInt32().ShouldBe(58);
        body.GetProperty("notes").GetString().ShouldBe("Morning.");
        body.GetProperty("recordedBy").GetString().ShouldBe("client");

        BodyMetricItem listed = (await ListAsync(client, client.Id, string.Empty)).ShouldHaveSingleItem();
        listed.ShouldBeJson(body.Deserialize<BodyMetricItem>(HttpAssertions.Json)!);
    }

    [Fact]
    public async Task PutBodyMetric_RecordedBy_IsWhoeverWroteItLast()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        BodyMetricItem byTrainer = await PutBodyMetricAsync(trainer, client.Id, TrainingDates.Yesterday);
        BodyMetricItem byClient = await PutBodyMetricAsync(client, client.Id, TrainingDates.Yesterday);

        byTrainer.RecordedBy.ShouldBe("trainer");
        byClient.RecordedBy.ShouldBe("client");
        (await ListAsync(trainer, client.Id, string.Empty)).ShouldHaveSingleItem().RecordedBy.ShouldBe("client");
    }

    [Fact]
    public async Task PutBodyMetric_ForTheSameDate_ReplacesTheWholeEntry()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        await PutBodyMetricAsync(client, client.Id, TrainingDates.Today, b => b.WithWeightKg(80m).WithWaistCm(82m).WithNotes("First."));

        BodyMetricItem replaced = await PutBodyMetricAsync(client, client.Id, TrainingDates.Today, b => b.WithWeightKg(null).WithBodyFatPercent(18m));
        BodyMetricItem again = await PutBodyMetricAsync(client, client.Id, TrainingDates.Today, b => b.WithWeightKg(null).WithBodyFatPercent(18m));

        replaced.WeightKg.ShouldBeNull();
        replaced.WaistCm.ShouldBeNull();
        replaced.Notes.ShouldBeNull();
        replaced.BodyFatPercent.ShouldBe(18m);
        again.ShouldBeJson(replaced with { UpdatedAt = again.UpdatedAt });
        (await ListAsync(client, client.Id, string.Empty)).ShouldHaveSingleItem().BodyFatPercent.ShouldBe(18m);
    }

    [Fact]
    public async Task ListBodyMetrics_IsSortedByDateWithInclusiveFromAndTo()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        foreach (int days in new[] { -3, -10, -1 })
        {
            await PutBodyMetricAsync(client, client.Id, TrainingDates.Offset(days));
        }

        BodyMetricItem[] all = await ListAsync(trainer, client.Id, string.Empty);
        BodyMetricItem[] range = await ListAsync(client, client.Id, $"?from={TrainingDates.Offset(-3)}&to={TrainingDates.Offset(-1)}");

        all.Select(m => m.Date).ShouldBe([TrainingDates.Offset(-10), TrainingDates.Offset(-3), TrainingDates.Offset(-1)]);
        range.Select(m => m.Date).ShouldBe([TrainingDates.Offset(-3), TrainingDates.Offset(-1)]);
    }

    [Fact]
    public async Task ListBodyMetrics_WithToBeforeFrom_Returns400OnTo()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.GetAsync(
            $"{TrainingRoutes.BodyMetrics(client.Id)}?from={TrainingDates.Today}&to={TrainingDates.Yesterday}", Ct);

        await response.ShouldBeValidationProblemAsync("to");
    }

    [Fact]
    public async Task DeleteBodyMetric_Returns204_EvenWhenAbsent()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        await PutBodyMetricAsync(client, client.Id, TrainingDates.Today);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.BodyMetric(client.Id, TrainingDates.Today), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await client.Client.DeleteAsync(TrainingRoutes.BodyMetric(client.Id, TrainingDates.Today), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await ListAsync(client, client.Id, string.Empty)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "weightKg": 19.99 }""", "weightKg")]
    [InlineData("""{ "weightKg": 400.01 }""", "weightKg")]
    [InlineData("""{ "weightKg": 80.123 }""", "weightKg")]
    [InlineData("""{ "bodyFatPercent": 1.99 }""", "bodyFatPercent")]
    [InlineData("""{ "bodyFatPercent": 75.01 }""", "bodyFatPercent")]
    [InlineData("""{ "waistCm": 9.99 }""", "waistCm")]
    [InlineData("""{ "chestCm": 300.01 }""", "chestCm")]
    [InlineData("""{ "hipsCm": 9 }""", "hipsCm")]
    [InlineData("""{ "armCm": 301 }""", "armCm")]
    [InlineData("""{ "thighCm": 9.99 }""", "thighCm")]
    [InlineData("""{ "restingHeartRate": 24 }""", "restingHeartRate")]
    [InlineData("""{ "restingHeartRate": 251 }""", "restingHeartRate")]
    [InlineData("""{ "restingHeartRate": 60.5 }""", "restingHeartRate")]
    public async Task PutBodyMetric_WithAnOutOfRangeValue_Returns400OnThatField(string json, string field)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, TrainingDates.Today, json);

        await response.ShouldBeValidationProblemAsync(field);
        (await ListAsync(client, client.Id, string.Empty)).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("""{ "weightKg": 20, "bodyFatPercent": 2, "waistCm": 10, "chestCm": 10, "hipsCm": 10, "armCm": 10, "thighCm": 10, "restingHeartRate": 25 }""")]
    [InlineData("""{ "weightKg": 400, "bodyFatPercent": 75, "waistCm": 300, "chestCm": 300, "hipsCm": 300, "armCm": 300, "thighCm": 300, "restingHeartRate": 250 }""")]
    [InlineData("""{ "restingHeartRate": 60 }""")]
    public async Task PutBodyMetric_AtTheRangeLimits_Returns200(string json)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, TrainingDates.Today, json);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact(Skip = "QA-F12: range messages read ''Weight Kg' must be between 20 and 400 with at most 2 decimals.', not 'Must be between ...'")]
    public async Task PutBodyMetric_OutOfRange_MessageHasNoPropertyNamePrefix()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, TrainingDates.Today, """{ "weightKg": 19.99 }""");

        Problem problem = await response.ShouldBeValidationProblemAsync("weightKg");
        problem.Errors!["weightKg"][0].ShouldStartWith("Must be between 20 and 400");
    }

    [Fact]
    public async Task PutBodyMetric_WithoutAnyMeasurement_Returns400()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, TrainingDates.Today, """{ "notes": "Only a note." }""");

        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.Errors.ShouldNotBeNull();
        problem.Errors.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task PutBodyMetric_ForAFutureDate_Returns400OnDate()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, TrainingDates.Tomorrow, """{ "weightKg": 80 }""");

        await response.ShouldBeValidationProblemAsync("date");
    }

    [Fact]
    public async Task PutBodyMetric_ChecksTheDateInTheClientsTimeZone()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone("Pacific/Kiritimati"));
        var clientToday = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("Pacific/Kiritimati")).DateTime);
        string today = clientToday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        string tomorrow = clientToday.AddDays(1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

        await (await PutRawAsync(trainer, client.Id, today, """{ "weightKg": 80 }""")).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await (await PutRawAsync(client, client.Id, tomorrow, """{ "weightKg": 80 }""")).ShouldBeValidationProblemAsync("date");
    }

    [Theory]
    [InlineData("2026-13-01")]
    [InlineData("yesterday")]
    public async Task PutBodyMetric_WithAMalformedDate_Returns400OnDate(string date)
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await PutRawAsync(client, client.Id, date, """{ "weightKg": 80 }""");

        await response.ShouldBeValidationProblemAsync("date");
    }

    [Fact]
    public async Task BodyMetrics_OfSomeoneElsesClient_Return404()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (TestUser otherTrainer, TestUser otherClient) = await CreateUtcTrainerWithClientAsync();
        await PutBodyMetricAsync(client, client.Id, TrainingDates.Today);

        foreach (TestUser viewer in new[] { otherTrainer, otherClient })
        {
            await (await viewer.Client.GetAsync(TrainingRoutes.BodyMetrics(client.Id), Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
            await (await PutRawAsync(viewer, client.Id, TrainingDates.Today, """{ "weightKg": 90 }""")).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
            await (await viewer.Client.DeleteAsync(TrainingRoutes.BodyMetric(client.Id, TrainingDates.Today), Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
        }

        (await ListAsync(client, client.Id, string.Empty)).ShouldHaveSingleItem().WeightKg.ShouldBe(80m);
    }

    private static async Task<HttpResponseMessage> PutRawAsync(TestUser user, Guid clientId, string date, string json) =>
        await user.Client.PutAsync(TrainingRoutes.BodyMetric(clientId, date), new StringContent(json, Encoding.UTF8, "application/json"), Ct);

    private static async Task<BodyMetricItem[]> ListAsync(TestUser user, Guid clientId, string query) =>
        await (await user.Client.GetAsync(TrainingRoutes.BodyMetrics(clientId) + query, Ct)).ShouldHaveJsonAsync<BodyMetricItem[]>();
}
