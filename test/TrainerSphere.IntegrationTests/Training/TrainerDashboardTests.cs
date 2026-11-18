using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 14: the trainer dashboard - today, adherence, recent records and sessions, and the pending invitations
/// read from the Training projection of Identity's invitations.
/// </summary>
public sealed class TrainerDashboardTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task TrainerDashboard_ForANewTrainer_HasTheContractShapeAndNoData()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        await Factory.WaitForMessagesAsync();

        JsonElement body = await DashboardAsync(trainer);

        body.ShouldHaveExactProperties("date", "activeClients", "today", "adherence", "recentPersonalRecords", "pendingInvitations", "recentSessions");
        body.GetProperty("date").GetString().ShouldBe(TrainingDates.Today);
        body.GetProperty("activeClients").GetInt32().ShouldBe(0);
        body.GetProperty("today").EnumerateArray().ShouldBeEmpty();
        body.GetProperty("adherence").EnumerateArray().ShouldBeEmpty();
        body.GetProperty("recentPersonalRecords").EnumerateArray().ShouldBeEmpty();
        body.GetProperty("recentSessions").EnumerateArray().ShouldBeEmpty();
        body.GetProperty("pendingInvitations").ShouldHaveExactProperties("count", "items");
        body.GetProperty("pendingInvitations").GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Today_ListsTodaysAssignmentsOfActiveClientsAndAdHocSessionsCompletedToday()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser alex = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser sam = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser gone = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        Guid exerciseId = workout.Exercises[0].ExerciseId;
        AssignmentItem done = await CreateAssignmentAsync(trainer, alex.Id, workout.Id);
        JsonElement completed = await CompleteAssignmentAsync(alex, done.Id, exerciseId);
        AssignmentItem scheduled = await CreateAssignmentAsync(trainer, sam.Id, workout.Id);
        await CreateAssignmentAsync(trainer, sam.Id, workout.Id, TrainingDates.Tomorrow);
        await CreateAssignmentAsync(trainer, sam.Id, workout.Id, TrainingDates.Yesterday);
        JsonElement adHoc = await CompleteAdHocSessionAsync(alex, Set(exerciseId, 50m, 5));
        await CompleteAdHocSessionStartedAtAsync(sam, DateTimeOffset.UtcNow.AddDays(-1).AddHours(-1), Set(exerciseId, 50m, 5));
        AssignmentItem goneToday = await CreateAssignmentAsync(trainer, gone.Id, workout.Id);
        await CompleteAssignmentAsync(gone, goneToday.Id, exerciseId);
        await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(gone.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        JsonElement body = await DashboardAsync(trainer);

        body.GetProperty("activeClients").GetInt32().ShouldBe(2);
        JsonElement[] today = [.. body.GetProperty("today").EnumerateArray()];
        today.Length.ShouldBe(3);
        foreach (JsonElement item in today)
        {
            item.ShouldHaveExactProperties("assignmentId", "client", "workoutName", "status", "sessionId", "completedAt");
            item.GetProperty("client").ShouldHaveExactProperties("id", "firstName", "lastName");
        }

        JsonElement doneItem = today.Single(i => i.GetProperty("assignmentId").ValueKind != JsonValueKind.Null && i.GetProperty("assignmentId").GetGuid() == done.Id);
        doneItem.GetProperty("status").GetString().ShouldBe("completed");
        doneItem.GetProperty("sessionId").GetGuid().ShouldBe(completed.GetProperty("id").GetGuid());
        doneItem.GetProperty("completedAt").GetDateTimeOffset().ShouldBe(completed.GetProperty("completedAt").GetDateTimeOffset());
        doneItem.GetProperty("client").GetProperty("id").GetGuid().ShouldBe(alex.Id);
        doneItem.GetProperty("workoutName").GetString().ShouldBe(workout.Name);

        JsonElement scheduledItem = today.Single(i => i.GetProperty("assignmentId").ValueKind != JsonValueKind.Null && i.GetProperty("assignmentId").GetGuid() == scheduled.Id);
        scheduledItem.GetProperty("status").GetString().ShouldBe("scheduled");
        scheduledItem.ShouldBeNullProperty("sessionId");
        scheduledItem.ShouldBeNullProperty("completedAt");

        JsonElement adHocItem = today.Single(i => i.GetProperty("assignmentId").ValueKind == JsonValueKind.Null);
        adHocItem.GetProperty("sessionId").GetGuid().ShouldBe(adHoc.GetProperty("id").GetGuid());
        adHocItem.GetProperty("status").GetString().ShouldBe("completed");
        adHocItem.GetProperty("workoutName").GetString().ShouldBe(adHoc.GetProperty("workoutName").GetString());
    }

    [Fact]
    public async Task Adherence_CoversActiveClientsWorstFirstWithNullsLast()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser behind = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser perfect = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser nothingDue = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        await CreateAdherenceHistoryAsync(trainer, behind);
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem done = await CreateAssignmentAsync(trainer, perfect.Id, workout.Id, TrainingDates.Yesterday);
        await CompleteAssignmentAsync(perfect, done.Id, workout.Exercises[0].ExerciseId);

        JsonElement[] adherence = [.. (await DashboardAsync(trainer)).GetProperty("adherence").EnumerateArray()];

        adherence.Select(a => a.GetProperty("client").GetProperty("id").GetGuid()).ShouldBe([behind.Id, perfect.Id, nothingDue.Id]);
        JsonElement first = adherence[0];
        first.ShouldHaveExactProperties("client", "adherence7d", "adherence28d", "due7d", "completed7d", "due28d", "completed28d", "lastSessionAt");
        first.GetProperty("adherence7d").GetRawText().ShouldBe("0.33");
        first.GetProperty("adherence28d").GetDecimal().ShouldBe(0.5m);
        (first.GetProperty("due7d").GetInt32(), first.GetProperty("completed7d").GetInt32()).ShouldBe((3, 1));
        (first.GetProperty("due28d").GetInt32(), first.GetProperty("completed28d").GetInt32()).ShouldBe((4, 2));
        first.GetProperty("lastSessionAt").ValueKind.ShouldBe(JsonValueKind.String);
        adherence[2].ShouldBeNullProperty("adherence7d");
        adherence[2].ShouldBeNullProperty("adherence28d");
        adherence[2].ShouldBeNullProperty("lastSessionAt");
        adherence[2].GetProperty("due28d").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task RecentPersonalRecords_AreTheLast14DaysNewestFirstAtMost20()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (_, TestUser stranger) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(client, "barbell-back-squat");
        DateTimeOffset now = DateTimeOffset.UtcNow;
        await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-30), Set(squat.Id, 50m, 8));
        JsonElement old = await CompleteAdHocSessionStartedAtAsync(client, now.AddDays(-20), Set(squat.Id, 55m, 8));
        old.GetProperty("summary").GetProperty("personalRecords").EnumerateArray().ShouldNotBeEmpty();
        await CompleteAdHocSessionAsync(stranger, Set(squat.Id, 50m, 8));
        await CompleteAdHocSessionAsync(stranger, Set(squat.Id, 90m, 8));

        Guid latest = Guid.Empty;
        for (int i = 0; i < 7; i++)
        {
            latest = (await CompleteAdHocSessionAsync(client, Set(squat.Id, 60m + (i * 2.5m), 8))).GetProperty("id").GetGuid();
        }

        JsonElement[] records = [.. (await DashboardAsync(trainer)).GetProperty("recentPersonalRecords").EnumerateArray()];

        records.Length.ShouldBe(20);
        records.ShouldAllBe(r => r.GetProperty("client").GetProperty("id").GetGuid() == client.Id);
        records.ShouldNotContain(r => r.GetProperty("sessionId").GetGuid() == old.GetProperty("id").GetGuid());
        records.Select(r => r.GetProperty("achievedAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
        records[0].GetProperty("sessionId").GetGuid().ShouldBe(latest);
        records[0].ShouldHaveExactProperties(
            "id", "clientId", "client", "exerciseId", "exercise", "type", "value", "previousValue", "weightKg", "reps", "sessionId", "achievedAt");
        records[0].GetProperty("client").ShouldHaveExactProperties("id", "firstName", "lastName");
    }

    [Fact]
    public async Task RecentSessions_AreTheTrainersTenNewestWithTheClient()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser alex = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        TestUser sam = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        (_, TestUser stranger) = await CreateUtcTrainerWithClientAsync();
        Exercise squat = await LibraryExerciseAsync(alex, "barbell-back-squat");
        await CompleteAdHocSessionAsync(stranger, Set(squat.Id, 50m, 5));
        DateTimeOffset now = DateTimeOffset.UtcNow;
        for (int i = 11; i >= 1; i--)
        {
            await CompleteAdHocSessionStartedAtAsync(i % 2 == 0 ? alex : sam, now.AddHours(-i), Set(squat.Id, 50m, 5));
        }

        JsonElement[] sessions = [.. (await DashboardAsync(trainer)).GetProperty("recentSessions").EnumerateArray()];
        JsonElement listed = (await (await sam.Client.GetAsync(TrainingRoutes.Sessions, Ct)).ReadJsonAsync()).GetProperty("items")[0];

        sessions.Length.ShouldBe(10);
        sessions.Select(s => s.GetProperty("startedAt").GetDateTimeOffset()).ShouldBeInOrder(SortDirection.Descending);
        sessions.ShouldAllBe(s => s.GetProperty("clientId").GetGuid() == alex.Id || s.GetProperty("clientId").GetGuid() == sam.Id);
        sessions[0].ShouldHaveExactProperties([.. listed.EnumerateObject().Select(p => p.Name), "client"]);
        sessions[0].GetProperty("client").GetProperty("id").GetGuid().ShouldBe(sessions[0].GetProperty("clientId").GetGuid());
    }

    [Fact(Skip = "QA-F13: dashboard invitation createdAt is the event time, not the invitation's createdAt from GET /api/invitations")]
    public async Task PendingInvitations_CountsAllAndListsTheTenNewestAsTheInvitationList()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        TestUser other = await CreateUtcTrainerAsync();
        await InviteAsync(other);
        for (int i = 0; i < 12; i++)
        {
            await InviteAsync(trainer);
        }

        await Factory.WaitForMessagesAsync();

        JsonElement pending = (await DashboardAsync(trainer)).GetProperty("pendingInvitations");
        Paged<Invitation> identity = await (await trainer.Client.GetAsync($"{IdentityRoutes.Invitations}?status=pending&pageSize=100", Ct))
            .ShouldHaveJsonAsync<Paged<Invitation>>();

        pending.GetProperty("count").GetInt32().ShouldBe(12);
        Invitation[] items = pending.GetProperty("items").Deserialize<Invitation[]>(HttpAssertions.Json)!;
        items.Length.ShouldBe(10);
        items.Select(i => i.CreatedAt).ShouldBeInOrder(SortDirection.Descending);
        items.ShouldBeJson(identity.Items.OrderByDescending(i => i.CreatedAt).Take(10).ToArray());
    }

    [Fact]
    public async Task PendingInvitations_ExcludeExpiredOnes()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        IssuedInvitation expired = await InviteAsync(trainer);
        await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();
        await ExpireAsync(expired.Invitation.Id);

        JsonElement pending = (await DashboardAsync(trainer)).GetProperty("pendingInvitations");

        pending.GetProperty("count").GetInt32().ShouldBe(1);
        pending.GetProperty("items").EnumerateArray().ShouldNotContain(i => i.GetProperty("id").GetGuid() == expired.Invitation.Id);
    }

    [Fact(Skip = "QA-F13: a resend overwrites the projected createdAt with the resend time, so the item and its order differ from GET /api/invitations")]
    public async Task PendingInvitations_AfterAResend_MatchTheInvitationList()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        IssuedInvitation first = await InviteAsync(trainer);
        await InviteAsync(trainer);
        await Task.Delay(20, Ct);
        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(first.Invitation.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await Factory.WaitForMessagesAsync();

        Invitation[] items = (await DashboardAsync(trainer)).GetProperty("pendingInvitations").GetProperty("items").Deserialize<Invitation[]>(HttpAssertions.Json)!;
        Paged<Invitation> identity = await (await trainer.Client.GetAsync($"{IdentityRoutes.Invitations}?status=pending", Ct)).ShouldHaveJsonAsync<Paged<Invitation>>();

        items.ShouldBeJson(identity.Items.OrderByDescending(i => i.CreatedAt).ToArray());
    }

    [Fact]
    public async Task PendingInvitations_AResentExpiredInvitationIsPendingAgain()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        IssuedInvitation invitation = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();
        await ExpireAsync(invitation.Invitation.Id);
        (await DashboardAsync(trainer)).GetProperty("pendingInvitations").GetProperty("count").GetInt32().ShouldBe(0);

        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(invitation.Invitation.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await Factory.WaitForMessagesAsync();

        JsonElement pending = (await DashboardAsync(trainer)).GetProperty("pendingInvitations");
        pending.GetProperty("count").GetInt32().ShouldBe(1);
        pending.GetProperty("items")[0].GetProperty("id").GetGuid().ShouldBe(invitation.Invitation.Id);
    }

    [Fact]
    public async Task Dashboards_ForTheOtherRole_Return403()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        await (await client.Client.GetAsync(TrainingRoutes.TrainerDashboard, Ct)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
        await (await trainer.Client.GetAsync(TrainingRoutes.ClientDashboard, Ct)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    private static async Task<JsonElement> DashboardAsync(TestUser trainer)
    {
        HttpResponseMessage response = await trainer.Client.GetAsync(TrainingRoutes.TrainerDashboard, Ct);
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        return await response.ReadJsonAsync();
    }

    private static async Task<JsonElement> CompleteAssignmentAsync(TestUser client, Guid assignmentId, Guid exerciseId)
    {
        Guid sessionId = (await StartSessionAsync(client, assignmentId)).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId);
        return await CompleteSessionAsync(client, sessionId);
    }

    /// <summary>
    /// Expiry needs a week to pass, so both the Identity row and its Training projection are moved into the past.
    /// </summary>
    private async Task ExpireAsync(Guid invitationId)
    {
        await Factory.ExecuteAsync("UPDATE identity.invitations SET expires_at = now() - interval '1 minute' WHERE id = @Id", new { Id = invitationId });
        await Factory.ExecuteAsync("UPDATE training.invitations SET expires_at = now() - interval '1 minute' WHERE id = @Id", new { Id = invitationId });
    }
}
