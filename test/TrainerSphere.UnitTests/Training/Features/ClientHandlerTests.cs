using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features;
using TrainerSphere.Api.Modules.Training.Features.Clients;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Training.Features;

public sealed class ClientHandlerTests
{
    private readonly IClientRepository _clients = Substitute.For<IClientRepository>();
    private readonly IAssignmentRepository _assignments = Substitute.For<IAssignmentRepository>();
    private readonly FakeDbSession _db = new();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(TrainingTestData.Now, TimeSpan.Zero));

    [Fact]
    public async Task A_client_cannot_list_clients()
    {
        var handler = new ListClientsHandler(_clients, _assignments, _db, _time);

        Result<Paged<ClientSummaryResponse>> result = await handler.Handle(
            new ListClientsQuery(Viewer.Client(TrainingTestData.ClientId, TrainingTestData.TrainerId), null, null, null, 1, 20),
            CancellationToken.None);

        ForbiddenError error = result.Value.ShouldBeOfType<ForbiddenError>();
        error.Code.ShouldBe("Client.ListForbidden");
    }

    [Fact]
    public async Task The_list_is_sorted_by_name_and_paged()
    {
        ClientProfile bravo = TrainingTestData.Client(id: Guid.NewGuid(), firstName: "Bravo", lastName: "Two");
        ClientProfile alpha = TrainingTestData.Client(id: Guid.NewGuid(), firstName: "alpha", lastName: "One");
        _clients.ListAsync(TrainingTestData.TrainerId, null, null, Arg.Any<CancellationToken>()).Returns([bravo, alpha]);
        var handler = new ListClientsHandler(_clients, _assignments, _db, _time);

        Result<Paged<ClientSummaryResponse>> result = await handler.Handle(
            new ListClientsQuery(Viewer.Trainer(TrainingTestData.TrainerId), null, null, "name", 2, 1),
            CancellationToken.None);

        Paged<ClientSummaryResponse> page = result.Value.ShouldBeOfType<Success<Paged<ClientSummaryResponse>>>().Value;
        page.TotalCount.ShouldBe(2);
        page.TotalPages.ShouldBe(2);
        page.Items.ShouldHaveSingleItem().FirstName.ShouldBe("Bravo");
    }

    [Fact]
    public async Task A_client_reads_their_own_detail_without_trainer_notes()
    {
        ClientProfile client = TrainingTestData.Client(goals: "Squat 120 kg", trainerNotes: "Left knee.");
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new GetClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new GetClientQuery(Viewer.Client(client.Id, client.TrainerId), client.Id),
            CancellationToken.None);

        ClientDetailResponse detail = result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value;
        detail.Goals.ShouldBe("Squat 120 kg");
        detail.TrainerNotes.ShouldBeNull();
    }

    [Fact]
    public async Task The_trainer_reads_the_detail_with_trainer_notes()
    {
        ClientProfile client = TrainingTestData.Client(trainerNotes: "Left knee.");
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new GetClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new GetClientQuery(Viewer.Trainer(TrainingTestData.TrainerId), client.Id),
            CancellationToken.None);

        result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value.TrainerNotes.ShouldBe("Left knee.");
    }

    [Fact]
    public async Task Another_trainers_client_is_not_found()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new GetClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new GetClientQuery(Viewer.Trainer(Guid.NewGuid()), client.Id),
            CancellationToken.None);

        result.Value.ShouldBeOfType<NotFoundError>().Code.ShouldBe("Client.NotFound");
    }

    [Fact]
    public async Task Updating_replaces_the_notes()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new UpdateClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new UpdateClientCommand(Viewer.Trainer(TrainingTestData.TrainerId), client.Id, "New goals", null),
            CancellationToken.None);

        result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value.Goals.ShouldBe("New goals");
        client.TrainerNotes.ShouldBeNull();
        await _clients.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archiving_removes_future_scheduled_assignments()
    {
        ClientProfile client = TrainingTestData.Client();
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        _assignments.DeleteScheduledFromAsync(client.Id, TrainingTestData.Today, Arg.Any<CancellationToken>()).Returns(3);
        var handler = new ArchiveClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new ArchiveClientCommand(TrainingTestData.TrainerId, client.Id),
            CancellationToken.None);

        ClientDetailResponse detail = result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value;
        detail.Status.ShouldBe(ClientStatus.Archived);
        await _assignments.Received(1).DeleteScheduledFromAsync(client.Id, TrainingTestData.Today, Arg.Any<CancellationToken>());
        await _clients.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Archiving_an_archived_client_is_idempotent()
    {
        ClientProfile client = TrainingTestData.Client(status: ClientStatus.Archived);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new ArchiveClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new ArchiveClientCommand(TrainingTestData.TrainerId, client.Id),
            CancellationToken.None);

        result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value.Status.ShouldBe(ClientStatus.Archived);
        await _assignments.DidNotReceive().DeleteScheduledFromAsync(Arg.Any<Guid>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Unarchiving_reactivates_the_client()
    {
        ClientProfile client = TrainingTestData.Client(status: ClientStatus.Archived);
        _clients.GetAsync(client.Id, Arg.Any<CancellationToken>()).Returns(client);
        var handler = new UnarchiveClientHandler(_clients, _assignments, _db, _time);

        Result<ClientDetailResponse> result = await handler.Handle(
            new UnarchiveClientCommand(TrainingTestData.TrainerId, client.Id),
            CancellationToken.None);

        result.Value.ShouldBeOfType<Success<ClientDetailResponse>>().Value.Status.ShouldBe(ClientStatus.Active);
        client.ArchivedAt.ShouldBeNull();
        await _clients.Received(1).UpdateAsync(client, Arg.Any<CancellationToken>());
    }
}
