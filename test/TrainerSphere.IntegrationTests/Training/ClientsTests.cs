using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 13: the trainer's clients - list (search, status, sorts with nulls last, paging), detail and notes,
/// next assignment, archive and unarchive, and access.
/// </summary>
public sealed class ClientsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    private static readonly string[] SummaryProperties =
        ["id", "firstName", "lastName", "email", "status", "joinedAt", "lastSessionAt", "adherence7d", "adherence28d", "nextAssignment"];

    [Theory]
    [InlineData("name", new[] { "Ada", "Ben", "Cy" })]
    [InlineData("-name", new[] { "Cy", "Ben", "Ada" })]
    [InlineData("adherence7d", new[] { "Cy", "Ben", "Ada" })]
    [InlineData("-adherence7d", new[] { "Ben", "Cy", "Ada" })]
    [InlineData("lastSessionAt", new[] { "Cy", "Ben", "Ada" })]
    [InlineData("-lastSessionAt", new[] { "Ben", "Cy", "Ada" })]
    public async Task ListClients_Sorts_WithNullsLast(string sort, string[] expected)
    {
        (TestUser trainer, _) = await CreateRosterAsync();

        Paged<ClientSummaryItem> page = await ListAsync(trainer, $"?sort={sort}");

        page.Items.Select(c => c.FirstName).ShouldBe(expected);
    }

    [Fact]
    public async Task ListClients_ReturnsTheContractShapeAndStats()
    {
        (TestUser trainer, IReadOnlyDictionary<string, TestUser> roster) = await CreateRosterAsync();

        JsonElement body = await (await trainer.Client.GetAsync($"{TrainingRoutes.Clients}?sort=name", Ct)).ReadJsonAsync();
        Paged<ClientSummaryItem> page = body.Deserialize<Paged<ClientSummaryItem>>(HttpAssertions.Json)!;

        foreach (JsonElement item in body.GetProperty("items").EnumerateArray())
        {
            item.ShouldHaveExactProperties(SummaryProperties);
        }

        ClientSummaryItem ada = page.Items.Single(c => c.Id == roster["Ada"].Id);
        ada.Email.ShouldBe(roster["Ada"].Email);
        ada.Status.ShouldBe("active");
        ada.LastSessionAt.ShouldBeNull();
        ada.Adherence7d.ShouldBeNull();
        ada.Adherence28d.ShouldBeNull();
        ada.NextAssignment.ShouldBeNull();
        ClientSummaryItem cy = page.Items.Single(c => c.Id == roster["Cy"].Id);
        cy.Adherence7d.ShouldBe(0.5m);
        cy.LastSessionAt.ShouldNotBeNull();
        page.Items.Single(c => c.Id == roster["Ben"].Id).Adherence7d.ShouldBe(1m);
    }

    [Fact]
    public async Task ListClients_RatesAreRoundedToTwoDecimals()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        await CreateAdherenceHistoryAsync(trainer, client);

        JsonElement item = (await (await trainer.Client.GetAsync(TrainingRoutes.Clients, Ct)).ReadJsonAsync()).GetProperty("items").EnumerateArray().Single();

        item.GetProperty("adherence7d").GetRawText().ShouldBe("0.33");
        item.GetProperty("adherence28d").GetDecimal().ShouldBe(0.5m);
    }

    [Fact]
    public async Task ListClients_SearchMatchesNamesAndEmailCaseInsensitively()
    {
        (TestUser trainer, IReadOnlyDictionary<string, TestUser> roster) = await CreateRosterAsync();
        string emailPart = roster["Ben"].Email.Split('@')[0].ToUpperInvariant();

        Paged<ClientSummaryItem> byName = await ListAsync(trainer, "?search=CY");
        Paged<ClientSummaryItem> byEmail = await ListAsync(trainer, $"?search={Uri.EscapeDataString(emailPart)}");

        byName.Items.ShouldHaveSingleItem().Id.ShouldBe(roster["Cy"].Id);
        byEmail.Items.ShouldHaveSingleItem().Id.ShouldBe(roster["Ben"].Id);
    }

    [Fact]
    public async Task ListClients_FiltersByStatusAndPages()
    {
        (TestUser trainer, IReadOnlyDictionary<string, TestUser> roster) = await CreateRosterAsync();
        await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(roster["Ben"].Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        Paged<ClientSummaryItem> active = await ListAsync(trainer, "?status=active&sort=name");
        Paged<ClientSummaryItem> archived = await ListAsync(trainer, "?status=archived");
        Paged<ClientSummaryItem> page = await ListAsync(trainer, "?status=active&sort=name&page=2&pageSize=1");

        active.Items.Select(c => c.FirstName).ShouldBe(["Ada", "Cy"]);
        archived.Items.ShouldHaveSingleItem().Status.ShouldBe("archived");
        page.TotalCount.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.Items.ShouldHaveSingleItem().FirstName.ShouldBe("Cy");
    }

    [Theory]
    [InlineData("status=Active", "status")]
    [InlineData("status=inactive", "status")]
    [InlineData("sort=email", "sort")]
    [InlineData("sort=Name", "sort")]
    public async Task ListClients_WithANonContractValue_Returns400MustBeOneOf(string query, string field)
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync($"{TrainingRoutes.Clients}?{query}", Ct);

        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors![field][0].ShouldStartWith("Must be one of: ");
    }

    [Fact]
    public async Task ListClients_AsAClient_Returns403()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.GetAsync(TrainingRoutes.Clients, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateClient_StoresGoalsAndNotes_AndOnlyTheTrainerSeesTheNotes()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Client(client.Id), new ClientUpdate("Squat 120 kg by March", "Left knee: avoid deep lunges."), HttpAssertions.Json, Ct);

        JsonElement detail = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        detail.ShouldHaveExactProperties([.. SummaryProperties, "goals", "trainerNotes", "inProgressSessionId"]);
        detail.GetProperty("goals").GetString().ShouldBe("Squat 120 kg by March");
        detail.GetProperty("trainerNotes").GetString().ShouldBe("Left knee: avoid deep lunges.");

        JsonElement own = await (await client.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ReadJsonAsync();
        own.ShouldHaveExactProperties([.. SummaryProperties, "goals", "trainerNotes", "inProgressSessionId"]);
        own.GetProperty("goals").GetString().ShouldBe("Squat 120 kg by March");
        own.ShouldBeNullProperty("trainerNotes");

        JsonElement reread = await (await trainer.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ReadJsonAsync();
        reread.GetProperty("trainerNotes").GetString().ShouldBe("Left knee: avoid deep lunges.");
    }

    [Theory]
    [InlineData(2001, 0, "goals")]
    [InlineData(0, 2001, "trainerNotes")]
    public async Task UpdateClient_WithTextOver2000Characters_Returns400OnThatField(int goals, int notes, string field)
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Client(client.Id), new ClientUpdate(new string('g', goals), new string('n', notes)), HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync(field);
    }

    [Fact]
    public async Task UpdateClient_With2000Characters_Returns200()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await trainer.Client.PutAsJsonAsync(
            TrainingRoutes.Client(client.Id), new ClientUpdate(new string('g', 2000), new string('n', 2000)), HttpAssertions.Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task UpdateClient_AsTheClient_Returns403()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.PutAsJsonAsync(TrainingRoutes.Client(client.Id), new ClientUpdate("Mine", "Sneaky"), HttpAssertions.Json, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetClient_OfSomeoneElse_Returns404ClientNotFound()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        (TestUser otherTrainer, TestUser otherClient) = await CreateUtcTrainerWithClientAsync();

        foreach (TestUser viewer in new[] { otherTrainer, otherClient })
        {
            await (await viewer.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
        }

        await (await otherTrainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct)).ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
    }

    [Fact]
    public async Task NextAssignment_IsTheFirstScheduledOnOrAfterToday()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Yesterday);
        AssignmentItem today = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Today);
        AssignmentItem tomorrow = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);

        ClientSummaryItem before = (await ListAsync(trainer, string.Empty)).Items.Single();
        await CompleteAsync(client, today.Id, workout.Exercises[0].ExerciseId);
        ClientSummaryItem after = (await ListAsync(trainer, string.Empty)).Items.Single();

        before.NextAssignment.ShouldBe(new NextAssignmentItem(today.Id, TrainingDates.Today, workout.Name));
        after.NextAssignment.ShouldBe(new NextAssignmentItem(tomorrow.Id, TrainingDates.Tomorrow, workout.Name));
    }

    [Fact]
    public async Task GetClient_InProgressSessionId_TracksTheOpenSession()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem assignment = await CreateAssignmentAsync(trainer, client.Id, workout.Id);
        Guid sessionId = (await StartSessionAsync(client, assignment.Id)).GetProperty("id").GetGuid();

        JsonElement open = await (await trainer.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ReadJsonAsync();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), workout.Exercises[0].ExerciseId);
        JsonElement completed = await CompleteSessionAsync(client, sessionId);
        JsonElement closed = await (await trainer.Client.GetAsync(TrainingRoutes.Client(client.Id), Ct)).ReadJsonAsync();

        open.GetProperty("inProgressSessionId").GetGuid().ShouldBe(sessionId);
        closed.ShouldBeNullProperty("inProgressSessionId");
        closed.GetProperty("lastSessionAt").GetDateTimeOffset().ShouldBe(completed.GetProperty("completedAt").GetDateTimeOffset());
    }

    [Fact]
    public async Task ArchiveClient_RemovesFutureScheduledAssignmentsAndKeepsHistory()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem missed = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(-2));
        AssignmentItem done = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Yesterday);
        await CompleteAsync(client, done.Id, workout.Exercises[0].ExerciseId);
        AssignmentItem future = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Offset(3));

        HttpResponseMessage response = await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct);

        JsonElement detail = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        detail.GetProperty("status").GetString().ShouldBe("archived");
        detail.ShouldBeNullProperty("nextAssignment");
        await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(future.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        (await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(missed.Id), Ct)).ShouldHaveJsonAsync<AssignmentItem>()).Status.ShouldBe("missed");
        (await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(done.Id), Ct)).ShouldHaveJsonAsync<AssignmentItem>()).Status.ShouldBe("completed");
    }

    [Fact]
    public async Task ArchiveAndUnarchive_AreIdempotentAndDoNotRestoreAssignments()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        AssignmentItem future = await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);

        await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        JsonElement again = await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct)).ReadJsonAsync();
        JsonElement unarchived = await (await trainer.Client.PostAsync(TrainingRoutes.ClientUnarchive(client.Id), null, Ct)).ReadJsonAsync();
        JsonElement twice = await (await trainer.Client.PostAsync(TrainingRoutes.ClientUnarchive(client.Id), null, Ct)).ReadJsonAsync();

        again.GetProperty("status").GetString().ShouldBe("archived");
        unarchived.GetProperty("status").GetString().ShouldBe("active");
        twice.GetProperty("status").GetString().ShouldBe("active");
        unarchived.ShouldBeNullProperty("nextAssignment");
        await (await trainer.Client.GetAsync(TrainingRoutes.Assignment(future.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NotFound);
        await CreateAssignmentAsync(trainer, client.Id, workout.Id, TrainingDates.Tomorrow);
    }

    [Fact]
    public async Task ArchiveClient_AsTheClient_Returns403()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();

        HttpResponseMessage response = await client.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    private static async Task CompleteAsync(TestUser client, Guid assignmentId, Guid exerciseId)
    {
        Guid sessionId = (await StartSessionAsync(client, assignmentId)).GetProperty("id").GetGuid();
        await LogSetAsync(client, sessionId, Guid.NewGuid(), exerciseId);
        await CompleteSessionAsync(client, sessionId);
    }

    private static async Task<Paged<ClientSummaryItem>> ListAsync(TestUser trainer, string query) =>
        await (await trainer.Client.GetAsync(TrainingRoutes.Clients + query, Ct)).ShouldHaveJsonAsync<Paged<ClientSummaryItem>>();

    /// <summary>
    /// Three clients: Ada (nothing due, no sessions), Cy (7-day adherence 0.5, the earlier session) and Ben
    /// (adherence 1.0, the latest session).
    /// </summary>
    private async Task<(TestUser Trainer, IReadOnlyDictionary<string, TestUser> Roster)> CreateRosterAsync()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        Dictionary<string, TestUser> roster = [];
        foreach (string name in new[] { "Cy", "Ada", "Ben" })
        {
            roster[name] = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC").WithFirstName(name).WithLastName(Unique.Name("Client")));
        }

        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        Guid exerciseId = workout.Exercises[0].ExerciseId;
        await CreateAssignmentAsync(trainer, roster["Cy"].Id, workout.Id, TrainingDates.Yesterday);
        AssignmentItem cyDone = await CreateAssignmentAsync(trainer, roster["Cy"].Id, workout.Id, TrainingDates.Offset(-2));
        await CompleteAsync(roster["Cy"], cyDone.Id, exerciseId);
        await Task.Delay(20, Ct);
        AssignmentItem benDone = await CreateAssignmentAsync(trainer, roster["Ben"].Id, workout.Id, TrainingDates.Yesterday);
        await CompleteAsync(roster["Ben"], benDone.Id, exerciseId);

        return (trainer, roster);
    }
}
