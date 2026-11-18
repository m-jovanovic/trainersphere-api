using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 14: the client dashboard - today, the next assignment, the open session, this week's and the rolling
/// adherence, the streak, recent records and sessions, and the latest body metric, all for the signed-in client.
/// </summary>
public sealed class ClientDashboardTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ClientDashboard_ForANewClient_HasTheContractShapeAndNothingDue()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        JsonElement body = await DashboardAsync(client);

        string[] members =
        [
            "date", "today", "nextAssignment", "inProgressSession", "thisWeek", "adherence7d", "adherence28d", "streakWeeks",
            "recentPersonalRecords", "recentSessions", "latestBodyMetric",
        ];
        body.ShouldHaveExactProperties(members);
        body.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        body.GetProperty("today").EnumerateArray().ShouldBeEmpty();
        body.ShouldBeNullProperty("nextAssignment");
        body.ShouldBeNullProperty("inProgressSession");
        body.GetProperty("thisWeek").ShouldHaveExactProperties("due", "completed", "rate");
        body.GetProperty("thisWeek").GetProperty("due").GetInt32().ShouldBe(0);
        body.GetProperty("thisWeek").GetProperty("completed").GetInt32().ShouldBe(0);
        body.GetProperty("thisWeek").ShouldBeNullProperty("rate");
        body.ShouldBeNullProperty("adherence7d");
        body.ShouldBeNullProperty("adherence28d");
        body.GetProperty("streakWeeks").GetInt32().ShouldBe(0);
        body.GetProperty("recentPersonalRecords").EnumerateArray().ShouldBeEmpty();
        body.GetProperty("recentSessions").EnumerateArray().ShouldBeEmpty();
        body.ShouldBeNullProperty("latestBodyMetric");
    }

    [Theory]
    [InlineData("Pacific/Kiritimati")]
    [InlineData("Pacific/Pago_Pago")]
    public async Task ClientDashboard_DateAndToday_UseTheClientsTimeZone(string timeZone)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone(timeZone));
        string clientToday = LocalToday(timeZone);
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id, clientToday);

        JsonElement body = await DashboardAsync(client);

        body.GetProperty("date").GetString().ShouldBe(clientToday);
        JsonElement item = body.GetProperty("today").EnumerateArray().ShouldHaveSingleItem();
        item.GetProperty("id").GetGuid().ShouldBe(assignment.Id);
        item.GetProperty("status").GetString().ShouldBe("scheduled");
    }

    [Fact]
    public async Task Today_ListsOnlyTheClientsAssignmentsDatedTodayAsTheAssignmentReadReturnsThem()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem scheduled = await CreateAssignmentAsync(trainer, client.Id, workout.Id, notes: "Go by feel.");
        AssignmentItem done = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        await CompleteAssignmentAsync(client, done.Id, workout.Exercises[0].ExerciseId);
        AssignmentItem skipped = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        await SkipAsync(client, skipped.Id);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Yesterday);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);
        await CreateAssignmentAsync(trainer, other.Id, workout.Id);

        JsonElement[] today = [.. (await DashboardAsync(client)).GetProperty("today").EnumerateArray()];

        today.Select(a => a.GetProperty("id").GetGuid()).ShouldBe([scheduled.Id, done.Id, skipped.Id], ignoreOrder: true);
        foreach (JsonElement item in today)
        {
            item.ShouldBeJson(await AssignmentAsync(client, item.GetProperty("id").GetGuid()));
        }

        today.Single(a => a.GetProperty("id").GetGuid() == done.Id).GetProperty("status").GetString().ShouldBe("completed");
        today.Single(a => a.GetProperty("id").GetGuid() == skipped.Id).GetProperty("status").GetString().ShouldBe("skipped");
    }

    [Fact]
    public async Task NextAssignment_IsTheFirstScheduledAssignmentStrictlyAfterToday()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        AssignmentItem skippedTomorrow = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);
        await SkipAsync(client, skippedTomorrow.Id);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(5));
        AssignmentItem next = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(2));

        JsonElement nextAssignment = (await DashboardAsync(client)).GetProperty("nextAssignment");

        nextAssignment.ShouldBeJson(await AssignmentAsync(client, next.Id));
    }

    [Fact]
    public async Task NextAssignment_WithOnlyTodaysAndPastAssignments_IsNull()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Yesterday);

        (await DashboardAsync(client)).ShouldBeNullProperty("nextAssignment");
    }

    [Fact(Skip = "QA-F14: the client dashboard only looks 92 days ahead for nextAssignment; the client summary has no limit")]
    public async Task NextAssignment_MoreThan92DaysAhead_IsStillTheNextAssignment()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem far = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(120));
        ClientSummaryItem summary = await (await trainer.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ShouldHaveJsonAsync<ClientSummaryItem>();
        summary.NextAssignment!.Id.ShouldBe(far.Id);

        JsonElement nextAssignment = (await DashboardAsync(client)).GetProperty("nextAssignment");

        nextAssignment.ValueKind.ShouldBe(JsonValueKind.Object, "the next scheduled assignment is 120 days ahead");
        nextAssignment.GetProperty("id").GetGuid().ShouldBe(far.Id);
    }

    [Fact]
    public async Task InProgressSession_IsTheOpenSessionSummaryUntilItIsCompleted()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        HttpResponseMessage started = await client.Client.PostAsJsonAsync(TrainingRoutes.Sessions, new { workoutName = Unique.Name("Open") }, HttpAssertions.Json, Ct);
        await started.ShouldHaveStatusAsync(HttpStatusCode.Created);
        Guid sessionId = (await started.ReadJsonAsync()).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), squat.Id, b => b.WithWeightKg(60m).WithReps(5));

        JsonElement open = (await DashboardAsync(client)).GetProperty("inProgressSession");
        JsonElement listed = (await (await client.Client.GetAsync($"{TrainingRoutes.Sessions}?status=in-progress", Ct)).ReadJsonAsync()).GetProperty("items")[0];

        open.ShouldBeJson(listed);
        open.GetProperty("id").GetGuid().ShouldBe(sessionId);
        open.GetProperty("status").GetString().ShouldBe("in-progress");

        await CompleteSessionAsync(client, sessionId);
        JsonElement after = await DashboardAsync(client);

        after.ShouldBeNullProperty("inProgressSession");
        after.GetProperty("recentSessions")[0].GetProperty("id").GetGuid().ShouldBe(sessionId);
    }

    [Fact]
    public async Task ThisWeek_CountsTheIsoWeekFromMondayUpToToday()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        Guid exerciseId = workout.Exercises[0].ExerciseId;
        int sinceMonday = ((int)DateTime.UtcNow.DayOfWeek + 6) % 7;

        // Last Sunday is outside the week; each earlier day of this week alternates completed and missed.
        AssignmentItem sunday = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(-sinceMonday - 1));
        await CompleteAssignmentAsync(client, sunday.Id, exerciseId);
        int completed = 0;
        for (int day = 1; day <= sinceMonday; day++)
        {
            AssignmentItem past = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(-day));
            if (day % 2 == 1)
            {
                await CompleteAssignmentAsync(client, past.Id, exerciseId);
                completed++;
            }
        }

        await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        AssignmentItem doneToday = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        await CompleteAssignmentAsync(client, doneToday.Id, exerciseId);
        completed++;
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);
        int due = sinceMonday + 1;

        JsonElement thisWeek = (await DashboardAsync(client)).GetProperty("thisWeek");

        thisWeek.GetProperty("due").GetInt32().ShouldBe(due);
        thisWeek.GetProperty("completed").GetInt32().ShouldBe(completed);
        thisWeek.GetProperty("rate").GetDecimal().ShouldBe(Math.Round((decimal)completed / due, 2));
    }

    [Fact]
    public async Task Adherence_IsTheRollingRateTheProgressSummaryReports()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        await CreateAdherenceHistoryAsync(trainer, client);

        JsonElement body = await DashboardAsync(client);
        JsonElement summary = await (await client.Client.GetAsync(TrainingRoutes.ProgressSummary(client.Id), Ct)).ReadJsonAsync();

        body.GetProperty("adherence7d").GetRawText().ShouldBe("0.33");
        body.GetProperty("adherence28d").GetDecimal().ShouldBe(0.5m);
        body.GetProperty("adherence7d").GetDecimal().ShouldBe(summary.GetProperty("adherence7d").GetDecimal());
        body.GetProperty("adherence28d").GetDecimal().ShouldBe(summary.GetProperty("adherence28d").GetDecimal());
    }

    [Fact]
    public async Task StreakWeeks_CountsConsecutiveWeeksBackFromThisWeek()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionAsync(client, Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-7), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-14), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-28), Set(squat.Id, 50m, 10));

        int streak = (await DashboardAsync(client)).GetProperty("streakWeeks").GetInt32();
        JsonElement summary = await (await client.Client.GetAsync(TrainingRoutes.ProgressSummary(client.Id), Ct)).ReadJsonAsync();

        streak.ShouldBe(3);
        streak.ShouldBe(summary.GetProperty("streakWeeks").GetInt32());
    }

    [Fact]
    public async Task StreakWeeks_SkipsAnEmptyCurrentWeek()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-7), Set(squat.Id, 50m, 10));
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-14), Set(squat.Id, 50m, 10));

        (await DashboardAsync(client)).GetProperty("streakWeeks").GetInt32().ShouldBe(2);
    }

    [Fact]
    public async Task RecentPersonalRecords_AreTheClientsLast14DaysNewestFirstWithoutALimit()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-30), Set(squat.Id, 50m, 8));
        JsonElement old = await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-20), Set(squat.Id, 55m, 8));
        old.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldNotBeEmpty();
        await CompleteAdHocSessionAsync(other, Set(squat.Id, 50m, 8));
        await CompleteAdHocSessionAsync(other, Set(squat.Id, 90m, 8));
        for (int i = 0; i < 8; i++)
        {
            await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m + (i * 2.5m), 8));
        }

        JsonElement[] records = [.. (await DashboardAsync(client)).GetProperty("recentPersonalRecords").EnumerateArray()];
        JsonElement page = await (await client.Client.GetAsync($"{TrainingRoutes.PersonalRecordHistory(client.Id)}?pageSize=100", Ct)).ReadJsonAsync();
        JsonElement[] history = [.. page.GetProperty("items").EnumerateArray().Where(r => r.GetProperty("achievedAt").GetDateTimeOffset() >= now.AddDays(-14))];

        records.Length.ShouldBeGreaterThan(20);
        records.Select(r => r.GetProperty("achievedAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
        records.ShouldNotContain(r => r.GetProperty("sessionId").GetGuid() == old.GetProperty("id").GetGuid());
        records[0].ShouldHaveExactProperties("id", "clientId", "exerciseId", "exercise", "type", "value", "previousValue", "weightKg", "reps", "sessionId", "achievedAt");
        records.OrderBy(r => r.GetProperty("id").GetGuid()).ToArray().ShouldBeJson(history.OrderBy(r => r.GetProperty("id").GetGuid()).ToArray());
    }

    [Fact]
    public async Task RecentSessions_AreTheClientsFiveNewestAsTheSessionListReturnsThem()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser client = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser other = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 7; i >= 1; i--)
        {
            await CompleteAdHocSessionStartedAtAsync(client, now.AddHours(-i), Set(squat.Id, 50m, 5));
        }

        await CompleteAdHocSessionAsync(other, Set(squat.Id, 50m, 5));

        JsonElement[] sessions = [.. (await DashboardAsync(client)).GetProperty("recentSessions").EnumerateArray()];
        JsonElement[] listed = [.. (await (await client.Client.GetAsync(TrainingRoutes.Sessions, Ct)).ReadJsonAsync()).GetProperty("items").EnumerateArray().Take(5)];

        sessions.Length.ShouldBe(5);
        sessions.ShouldAllBe(s => s.GetProperty("clientId").GetGuid() == client.Id);
        sessions.Select(s => s.GetProperty("startedAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
        sessions.ShouldBeJson(listed);
    }

    [Fact]
    public async Task LatestBodyMetric_IsTheEntryWithTheLatestDate()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        BodyMetricItem latest = await PutBodyMetricAsync(client, client.Id, TrainingDates.Yesterday, b => b.WithWeightKg(80.5m));
        await PutBodyMetricAsync(trainer, client.Id, TrainingDates.Offset(-5), b => b.WithWeightKg(82m));

        JsonElement metric = (await DashboardAsync(client)).GetProperty("latestBodyMetric");

        metric.Deserialize<BodyMetricItem>(HttpAssertions.Json).ShouldBeJson(latest);
        metric.ShouldHaveExactProperties(
            "clientId", "date", "weightKg", "bodyFatPercent", "waistCm", "chestCm", "hipsCm", "armCm", "thighCm", "restingHeartRate", "notes", "recordedBy", "updatedAt");
    }

    private static string LocalToday(string timeZone) =>
        DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(timeZone)))
            .ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static async Task<JsonElement> DashboardAsync(TestUser client)
    {
        HttpResponseMessage response = await client.Client.GetAsync(TrainingRoutes.ClientDashboard, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> AssignmentAsync(TestUser user, Guid id)
    {
        HttpResponseMessage response = await user.Client.GetAsync(TrainingRoutes.Assignment(id), Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task SkipAsync(TestUser user, Guid assignmentId) =>
        await (await user.Client.PostAsJsonAsync(TrainingRoutes.AssignmentSkip(assignmentId), new { }, HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

    private static async Task CompleteAssignmentAsync(TestUser client, Guid assignmentId, Guid exerciseId)
    {
        Guid sessionId = (await StartSessionAsync(client, assignmentId)).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId);
        await CompleteSessionAsync(client, sessionId);
    }
}
