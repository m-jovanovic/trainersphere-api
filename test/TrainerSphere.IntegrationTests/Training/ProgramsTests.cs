using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// Section 8: programs - CRUD, the slot grid, assigning programs to clients, program-assignment removal.
/// </summary>
public sealed class ProgramsTests(ApiFactory factory) : TrainingTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateProgram_Returns201WithSlotsSortedByWeekDayThenInputOrder()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        WorkoutItem first = await CreateWorkoutAsync(trainer);
        WorkoutItem second = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Programs,
            new ProgramBuilder()
                .WithWeeks(2)
                .WithSlot(2, 1, second.Id)
                .WithSlot(1, 3, first.Id)
                .WithSlot(1, 1, second.Id)
                .WithSlot(1, 1, first.Id)
                .Build(),
            HttpAssertions.Json,
            Ct);

        JsonElement body = await response.ReadJsonAsync();
        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        body.ShouldHaveExactProperties("id", "name", "description", "weeks", "archived", "createdAt", "updatedAt", "slots");
        body.ShouldHaveAtMostMicroseconds("createdAt");
        body.GetProperty("weeks").GetInt32().ShouldBe(2);

        JsonElement[] slots = [.. body.GetProperty("slots").EnumerateArray()];
        slots.Select(s => (s.GetProperty("week").GetInt32(), s.GetProperty("day").GetInt32())).ShouldBe([(1, 1), (1, 1), (1, 3), (2, 1)]);
        slots[0].GetProperty("workoutId").GetGuid().ShouldBe(second.Id);
        slots[1].GetProperty("workoutId").GetGuid().ShouldBe(first.Id);
        slots[0].GetProperty("workout").ShouldHaveExactProperties("id", "name");
        slots[0].GetProperty("workout").GetProperty("name").GetString().ShouldBe(second.Name);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(53)]
    public async Task CreateProgram_WithWeeksOutOfRange_Returns400(int weeks)
    {
        TestUser trainer = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Programs, new ProgramBuilder().WithWeeks(weeks).Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeValidationProblemAsync("weeks");
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(3, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 8)]
    public async Task CreateProgram_WithASlotOutsideTheGrid_Returns422SlotOutOfRange(int week, int day)
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.Programs,
            new ProgramBuilder().WithWeeks(2).WithSlot(week, day, workout.Id).Build(),
            HttpAssertions.Json,
            Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Program.SlotOutOfRange");
    }

    [Fact]
    public async Task CreateProgram_WithFourSlotsOnOneDay_Returns422TooManySlots()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        ProgramBuilder builder = new ProgramBuilder().WithWeeks(1);
        for (int i = 0; i < 4; i++)
        {
            builder.WithSlot(1, 1, workout.Id);
        }

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(TrainingRoutes.Programs, builder.Build(), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Program.TooManySlots");
    }

    [Fact]
    public async Task UpdateProgram_FullReplace_DoesNotTouchCreatedAssignments()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem first = await CreateWorkoutAsync(trainer);
        WorkoutItem second = await CreateWorkoutAsync(trainer);
        ProgramItem program = await CreateProgramAsync(trainer, b => b.WithWeeks(1).WithSlot(1, 2, first.Id));
        string start = TrainingDates.Today;
        await (await trainer.Client.PostAsJsonAsync(
                TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([client.Id], start), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);

        ProgramItem updated = await (await trainer.Client.PutAsJsonAsync(
                TrainingRoutes.Program(program.Id),
                new ProgramBuilder().WithName(Unique.Name("Program")).WithWeeks(2).WithSlot(2, 5, second.Id).Build(),
                HttpAssertions.Json,
                Ct))
            .ShouldHaveJsonAsync<ProgramItem>();

        updated.Weeks.ShouldBe(2);
        updated.Slots.Single().WorkoutId.ShouldBe(second.Id);

        AssignmentItem[] assignments = await (await trainer.Client.GetAsync($"{TrainingRoutes.Assignments}?clientId={client.Id}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();
        assignments.Single().WorkoutId.ShouldBe(first.Id);
        assignments.Single().Date.ShouldBe(TrainingDates.Offset(1));
    }

    [Fact]
    public async Task DeleteProgram_ArchivesIt()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        ProgramItem program = await CreateProgramAsync(trainer);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.Program(program.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        Paged<ProgramSummary> list = await (await trainer.Client.GetAsync(TrainingRoutes.Programs, Ct)).ShouldHaveJsonAsync<Paged<ProgramSummary>>();
        list.Items.ShouldNotContain(p => p.Id == program.Id);
        Paged<ProgramSummary> withArchived = await (await trainer.Client.GetAsync($"{TrainingRoutes.Programs}?includeArchived=true", Ct))
            .ShouldHaveJsonAsync<Paged<ProgramSummary>>();
        withArchived.Items.ShouldContain(p => p.Id == program.Id && p.Archived);
    }

    [Fact]
    public async Task AssignProgram_CreatesOneAssignmentPerSlotPerClient()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser second = await CreateClientAsync(trainer, b => b.WithTimeZone("UTC"));
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        ProgramItem program = await CreateProgramAsync(trainer, b => b
            .WithWeeks(2)
            .WithSlot(1, 1, workout.Id)
            .WithSlot(1, 3, workout.Id)
            .WithSlot(2, 2, workout.Id));
        string start = TrainingDates.Tomorrow;

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([client.Id, second.Id], start), HttpAssertions.Json, Ct);

        ProgramAssignmentItem[] assigned = await response.ShouldHaveJsonAsync<ProgramAssignmentItem[]>(HttpStatusCode.Created);
        assigned.Length.ShouldBe(2);
        ProgramAssignmentItem forClient = assigned.Single(a => a.ClientId == client.Id);
        forClient.ProgramId.ShouldBe(program.Id);
        forClient.ProgramName.ShouldBe(program.Name);
        forClient.StartDate.ShouldBe(start);
        forClient.EndDate.ShouldBe(TrainingDates.Offset(14)); // tomorrow + 2 weeks - 1 day
        forClient.AssignmentCount.ShouldBe(3);

        AssignmentItem[] assignments = await (await trainer.Client.GetAsync(
                $"{TrainingRoutes.Assignments}?clientId={client.Id}&from={start}&to={TrainingDates.Offset(20)}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();
        assignments.Select(a => a.Date).ShouldBe([start, TrainingDates.Offset(3), TrainingDates.Offset(9)]);
        assignments.ShouldAllBe(a => a.Status == "scheduled" && a.ProgramAssignmentId == forClient.Id && a.ProgramName == program.Name);
    }

    [Fact]
    public async Task AssignProgram_ToAnArchivedClient_Returns422ClientArchived()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        ProgramItem program = await CreateProgramAsync(trainer);
        await (await trainer.Client.PostAsync(TrainingRoutes.ClientArchive(client.Id), null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([client.Id], TrainingDates.Tomorrow), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnprocessableEntity, "Client.Archived");
    }

    [Fact]
    public async Task AssignProgram_ToAnUnknownClient_Returns404()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        ProgramItem program = await CreateProgramAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([Guid.NewGuid()], TrainingDates.Tomorrow), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
    }

    [Fact]
    public async Task AssignProgram_ForAnotherTrainersProgram_Returns404()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateUtcTrainerAsync();
        ProgramItem foreign = await CreateProgramAsync(other);

        HttpResponseMessage response = await trainer.Client.PostAsJsonAsync(
            TrainingRoutes.ProgramAssign(foreign.Id), new AssignProgramRequest([client.Id], TrainingDates.Tomorrow), HttpAssertions.Json, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Program.NotFound");
    }

    [Fact]
    public async Task ListProgramAssignments_ReturnsThemForTheTrainerOnly()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        ProgramItem program = await CreateProgramAsync(trainer);
        await (await trainer.Client.PostAsJsonAsync(
                TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([client.Id], TrainingDates.Tomorrow), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Created);

        ProgramAssignmentItem[] forTrainer = await (await trainer.Client.GetAsync(TrainingRoutes.ProgramAssignmentsOf(client.Id), Ct))
            .ShouldHaveJsonAsync<ProgramAssignmentItem[]>();
        forTrainer.Single().ProgramId.ShouldBe(program.Id);

        // Section 8 is trainer-only; a client reads their assignments, not the program projection.
        await (await client.Client.GetAsync(TrainingRoutes.ProgramAssignmentsOf(client.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListProgramAssignments_OfAnotherTrainersClient_Returns404()
    {
        (_, TestUser client) = await CreateUtcTrainerWithClientAsync();
        TestUser other = await CreateUtcTrainerAsync();

        HttpResponseMessage response = await other.Client.GetAsync(TrainingRoutes.ProgramAssignmentsOf(client.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Client.NotFound");
    }

    [Fact]
    public async Task DeleteProgramAssignment_RemovesTodaysAndFutureScheduledAssignmentsOnly()
    {
        (TestUser trainer, TestUser client) = await CreateUtcTrainerWithClientAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        ProgramItem program = await CreateProgramAsync(trainer, b => b
            .WithWeeks(1)
            .WithSlot(1, 1, workout.Id)
            .WithSlot(1, 2, workout.Id)
            .WithSlot(1, 3, workout.Id));
        ProgramAssignmentItem[] assigned = await (await trainer.Client.PostAsJsonAsync(
                TrainingRoutes.ProgramAssign(program.Id), new AssignProgramRequest([client.Id], TrainingDates.Yesterday), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<ProgramAssignmentItem[]>(HttpStatusCode.Created);

        await (await trainer.Client.DeleteAsync(TrainingRoutes.ProgramAssignment(assigned[0].Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        AssignmentItem[] remaining = await (await trainer.Client.GetAsync(
                $"{TrainingRoutes.Assignments}?clientId={client.Id}&from={TrainingDates.Yesterday}&to={TrainingDates.Offset(2)}", Ct))
            .ShouldHaveJsonAsync<AssignmentItem[]>();
        remaining.Length.ShouldBe(1);
        remaining[0].Date.ShouldBe(TrainingDates.Yesterday);
        remaining[0].Status.ShouldBe("missed");

        await (await trainer.Client.DeleteAsync(TrainingRoutes.ProgramAssignment(assigned[0].Id), Ct))
            .ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "ProgramAssignment.NotFound");
    }

    [Fact]
    public async Task GetProgram_ForAnotherTrainer_Returns404()
    {
        TestUser owner = await CreateUtcTrainerAsync();
        TestUser other = await CreateUtcTrainerAsync();
        ProgramItem program = await CreateProgramAsync(owner);

        HttpResponseMessage response = await other.Client.GetAsync(TrainingRoutes.Program(program.Id), Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "Program.NotFound");
    }

    [Fact]
    public async Task ListPrograms_SummariesCarryWeeksAndWorkoutCount()
    {
        TestUser trainer = await CreateUtcTrainerAsync();
        WorkoutItem workout = await CreateWorkoutAsync(trainer);
        ProgramItem program = await CreateProgramAsync(trainer, b => b
            .WithWeeks(3)
            .WithSlot(1, 1, workout.Id)
            .WithSlot(2, 1, workout.Id));

        Paged<ProgramSummary> list = await (await trainer.Client.GetAsync($"{TrainingRoutes.Programs}?search={program.Name}", Ct))
            .ShouldHaveJsonAsync<Paged<ProgramSummary>>();

        ProgramSummary summary = list.Items.Single();
        summary.Weeks.ShouldBe(3);
        summary.WorkoutCount.ShouldBe(2);
        summary.Archived.ShouldBeFalse();
    }
}
