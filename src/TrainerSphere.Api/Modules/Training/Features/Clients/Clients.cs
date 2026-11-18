using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Clients;

internal sealed record UpdateClientRequest(string? Goals, string? TrainerNotes);

internal sealed record ClientStats(DateTime? LastSessionAt, AdherenceStats Adherence7d, AdherenceStats Adherence28d, NextAssignmentRef? NextAssignment);

internal static class ClientViews
{
    public static ClientSummaryResponse ToSummary(ClientProfile client, ClientStats stats) =>
        new(
            client.Id,
            client.FirstName,
            client.LastName,
            client.Email,
            client.Status,
            client.JoinedAt,
            stats.LastSessionAt,
            stats.Adherence7d.Rate,
            stats.Adherence28d.Rate,
            stats.NextAssignment);

    public static ClientDetailResponse ToDetail(ClientProfile client, ClientStats stats, Guid? inProgressSessionId, bool includeTrainerNotes) =>
        new(
            client.Id,
            client.FirstName,
            client.LastName,
            client.Email,
            client.Status,
            client.JoinedAt,
            stats.LastSessionAt,
            stats.Adherence7d.Rate,
            stats.Adherence28d.Rate,
            stats.NextAssignment,
            client.Goals,
            includeTrainerNotes ? client.TrainerNotes : null,
            inProgressSessionId);
}

/// <summary>
/// Computes the per-client list statistics (last session, adherence, next assignment) in bulk, so a page of
/// clients costs three queries no matter its size. Each client is measured against their own "today".
/// </summary>
internal static class ClientStatsReader
{
    public static async Task<IReadOnlyDictionary<Guid, ClientStats>> ReadAsync(
        IDbSession db,
        IAssignmentRepository assignments,
        Guid trainerId,
        IReadOnlyList<ClientProfile> clients,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (clients.Count == 0)
        {
            return new Dictionary<Guid, ClientStats>();
        }

        Guid[] ids = [.. clients.Select(c => c.Id)];
        var todays = clients.ToDictionary(c => c.Id, c => timeProvider.Today(c.TimeZone));
        DateOnly minToday = todays.Values.Min();
        DateOnly maxToday = todays.Values.Max();

        IReadOnlyList<LastSessionRow> lastSessions = await db.QueryAsync<LastSessionRow>(
            """
            SELECT client_id, max(completed_at) AS last_session_at
            FROM training.sessions
            WHERE trainer_id = @TrainerId AND status = 'Completed' AND client_id = ANY(@ClientIds)
            GROUP BY client_id
            """,
            new { TrainerId = trainerId, ClientIds = ids },
            cancellationToken);

        IReadOnlyList<NextAssignmentRow> nextRows = await db.QueryAsync<NextAssignmentRow>(
            """
            SELECT client_id, id, date, workout->>'name' AS workout_name
            FROM training.assignments
            WHERE trainer_id = @TrainerId AND status = 'Scheduled' AND date >= @From AND client_id = ANY(@ClientIds)
            ORDER BY client_id, date, created_at, id
            """,
            new { TrainerId = trainerId, From = minToday.AddDays(-1), ClientIds = ids },
            cancellationToken);

        IReadOnlyList<Assignment> window = await assignments.ListAsync(trainerId, clientId: null, minToday.AddDays(-27), maxToday, cancellationToken);
        var itemsByClient = window
            .GroupBy(a => a.ClientId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduledItem>)[.. g.Select(a => new ScheduledItem(a.Date, a.StoredStatus))]);
        var lastSessionByClient = lastSessions.ToDictionary(r => r.ClientId, r => (DateTime?)r.LastSessionAt);
        var nextByClient = nextRows.GroupBy(r => r.ClientId).ToDictionary(g => g.Key, g => g.ToList());

        return clients.ToDictionary(
            c => c.Id,
            c =>
            {
                DateOnly today = todays[c.Id];
                IReadOnlyList<ScheduledItem> items = itemsByClient.GetValueOrDefault(c.Id, []);
                NextAssignmentRef? next = nextByClient
                    .GetValueOrDefault(c.Id, [])
                    .Where(r => r.Date >= today)
                    .Select(r => new NextAssignmentRef(r.Id, r.Date, r.WorkoutName))
                    .FirstOrDefault();

                return new ClientStats(
                    lastSessionByClient.GetValueOrDefault(c.Id),
                    Adherence.LastDays(items, today, 7),
                    Adherence.LastDays(items, today, 28),
                    next);
            });
    }

    public static Task<Guid?> GetInProgressSessionIdAsync(IDbSession db, Guid clientId, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT id FROM training.sessions WHERE client_id = @ClientId AND status = 'InProgress' ORDER BY started_at DESC, id DESC LIMIT 1",
            new { ClientId = clientId },
            cancellationToken);

    internal sealed record LastSessionRow
    {
        public Guid ClientId { get; init; }

        public DateTime LastSessionAt { get; init; }
    }

    internal sealed record NextAssignmentRow
    {
        public Guid ClientId { get; init; }

        public Guid Id { get; init; }

        public DateOnly Date { get; init; }

        public string WorkoutName { get; init; } = string.Empty;
    }
}

internal static class ClientDetails
{
    public static async Task<ClientDetailResponse> LoadAsync(
        Viewer viewer,
        ClientProfile client,
        IAssignmentRepository assignments,
        IDbSession db,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ClientStats> stats = await ClientStatsReader.ReadAsync(db, assignments, client.TrainerId, [client], timeProvider, cancellationToken);
        Guid? inProgressSessionId = await ClientStatsReader.GetInProgressSessionIdAsync(db, client.Id, cancellationToken);

        return ClientViews.ToDetail(client, stats[client.Id], inProgressSessionId, viewer.IsTrainer);
    }
}

internal sealed record ListClientsQuery(Viewer Viewer, string? Search, string? Status, string? Sort, int Page, int PageSize)
    : IQuery<Paged<ClientSummaryResponse>>, IPagedQuery;

internal sealed class ListClientsValidator : PagedQueryValidator<ListClientsQuery>
{
    public const string DefaultSort = "name";

    private static readonly string[] AllowedSorts = ["name", "-name", "lastSessionAt", "-lastSessionAt", "adherence7d", "-adherence7d"];

    public ListClientsValidator()
    {
        RuleFor(q => q.Search).MaximumLength(TrainingRules.SearchMaxLength);
        RuleFor(q => q.Status).KebabEnum<ListClientsQuery, ClientStatus>();
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || AllowedSorts.Contains(sort, StringComparer.Ordinal))
            .WithMessage($"'{{PropertyName}}' must be one of: {string.Join(", ", AllowedSorts)}.");
    }
}

internal sealed class ListClientsHandler(IClientRepository clients, IAssignmentRepository assignments, IDbSession db, TimeProvider timeProvider)
    : IQueryHandler<ListClientsQuery, Paged<ClientSummaryResponse>>
{
    public async Task<Result<Paged<ClientSummaryResponse>>> Handle(ListClientsQuery query, CancellationToken cancellationToken)
    {
        if (query.Viewer.IsClient)
        {
            return ClientErrors.ListForbidden;
        }

        ClientStatus? status = KebabCase.TryParse(query.Status, out ClientStatus parsed) ? parsed : null;
        IReadOnlyList<ClientProfile> found = await clients.ListAsync(query.Viewer.UserId, TrainingRules.LikePattern(query.Search), status?.ToString(), cancellationToken);
        IReadOnlyDictionary<Guid, ClientStats> stats = await ClientStatsReader.ReadAsync(db, assignments, query.Viewer.UserId, found, timeProvider, cancellationToken);

        IEnumerable<(ClientProfile Client, ClientSummaryResponse Summary)> rows = found.Select(c => (Client: c, Summary: ClientViews.ToSummary(c, stats[c.Id])));
        IOrderedEnumerable<(ClientProfile Client, ClientSummaryResponse Summary)> sorted = query.Sort switch
        {
            "-name" => rows.OrderByDescending(r => r.Client.FirstName, StringComparer.OrdinalIgnoreCase).ThenByDescending(r => r.Client.LastName, StringComparer.OrdinalIgnoreCase),
            "lastSessionAt" => rows.OrderBy(r => r.Summary.LastSessionAt is null).ThenBy(r => r.Summary.LastSessionAt),
            "-lastSessionAt" => rows.OrderBy(r => r.Summary.LastSessionAt is null).ThenByDescending(r => r.Summary.LastSessionAt),
            "adherence7d" => rows.OrderBy(r => r.Summary.Adherence7d is null).ThenBy(r => r.Summary.Adherence7d),
            "-adherence7d" => rows.OrderBy(r => r.Summary.Adherence7d is null).ThenByDescending(r => r.Summary.Adherence7d),
            _ => rows.OrderBy(r => r.Client.FirstName, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Client.LastName, StringComparer.OrdinalIgnoreCase),
        };

        var page = sorted.ThenBy(r => r.Client.Id).ToList();

        return new Paged<ClientSummaryResponse>(
            [.. page.Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).Select(r => r.Summary)],
            query.Page,
            query.PageSize,
            page.Count);
    }
}

internal sealed record GetClientQuery(Viewer Viewer, Guid ClientId) : IQuery<ClientDetailResponse>;

internal sealed class GetClientHandler(IClientRepository clients, IAssignmentRepository assignments, IDbSession db, TimeProvider timeProvider)
    : IQueryHandler<GetClientQuery, ClientDetailResponse>
{
    public Task<Result<ClientDetailResponse>> Handle(GetClientQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(client => ClientDetails.LoadAsync(query.Viewer, client, assignments, db, timeProvider, cancellationToken));
}

internal sealed record UpdateClientCommand(Viewer Viewer, Guid ClientId, string? Goals, string? TrainerNotes) : ICommand<ClientDetailResponse>;

internal sealed class UpdateClientValidator : AbstractValidator<UpdateClientCommand>
{
    public UpdateClientValidator()
    {
        RuleFor(c => c.Goals).MaximumLength(ClientProfile.NotesMaxLength);
        RuleFor(c => c.TrainerNotes).MaximumLength(ClientProfile.NotesMaxLength);
    }
}

internal sealed class UpdateClientHandler(IClientRepository clients, IAssignmentRepository assignments, IDbSession db, TimeProvider timeProvider)
    : ICommandHandler<UpdateClientCommand, ClientDetailResponse>
{
    public Task<Result<ClientDetailResponse>> Handle(UpdateClientCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(command.Viewer, command.ClientId, cancellationToken)
            .Tap(client =>
            {
                client.UpdateNotes(command.Goals, command.TrainerNotes, timeProvider.UtcNow());
                return clients.UpdateAsync(client, cancellationToken);
            })
            .Map(client => ClientDetails.LoadAsync(command.Viewer, client, assignments, db, timeProvider, cancellationToken));
}

internal sealed record ArchiveClientCommand(Guid TrainerId, Guid ClientId) : ICommand<ClientDetailResponse>;

internal sealed class ArchiveClientHandler(IClientRepository clients, IAssignmentRepository assignments, IDbSession db, TimeProvider timeProvider)
    : ICommandHandler<ArchiveClientCommand, ClientDetailResponse>
{
    public Task<Result<ClientDetailResponse>> Handle(ArchiveClientCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(Viewer.Trainer(command.TrainerId), command.ClientId, cancellationToken)
            .Bind(client => ArchiveAsync(command, client, cancellationToken));

    private async Task<Result<ClientDetailResponse>> ArchiveAsync(ArchiveClientCommand command, ClientProfile client, CancellationToken cancellationToken)
    {
        if (client.Archive(timeProvider.UtcNow()))
        {
            await assignments.DeleteScheduledFromAsync(client.Id, timeProvider.Today(client.TimeZone), cancellationToken);
            await clients.UpdateAsync(client, cancellationToken);
        }

        return await ClientDetails.LoadAsync(Viewer.Trainer(command.TrainerId), client, assignments, db, timeProvider, cancellationToken);
    }
}

internal sealed record UnarchiveClientCommand(Guid TrainerId, Guid ClientId) : ICommand<ClientDetailResponse>;

internal sealed class UnarchiveClientHandler(IClientRepository clients, IAssignmentRepository assignments, IDbSession db, TimeProvider timeProvider)
    : ICommandHandler<UnarchiveClientCommand, ClientDetailResponse>
{
    public Task<Result<ClientDetailResponse>> Handle(UnarchiveClientCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(Viewer.Trainer(command.TrainerId), command.ClientId, cancellationToken)
            .Bind(client => UnarchiveAsync(command, client, cancellationToken));

    private async Task<Result<ClientDetailResponse>> UnarchiveAsync(UnarchiveClientCommand command, ClientProfile client, CancellationToken cancellationToken)
    {
        if (client.Unarchive(timeProvider.UtcNow()))
        {
            await clients.UpdateAsync(client, cancellationToken);
        }

        return await ClientDetails.LoadAsync(Viewer.Trainer(command.TrainerId), client, assignments, db, timeProvider, cancellationToken);
    }
}

internal sealed class ClientEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/clients").WithTags("Clients");

        group.MapGet(
                string.Empty,
                (string? search, string? status, string? sort, int? page, int? pageSize, IQueryHandler<ListClientsQuery, Paged<ClientSummaryResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListClientsQuery(http.User.ToViewer(), search, status, sort ?? ListClientsValidator.DefaultSort, page ?? 1, pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ToOk())
            .WithName("ListClients")
            .RequirePermission(Permissions.ClientsRead);

        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetClientQuery, ClientDetailResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetClientQuery(http.User.ToViewer(), id), cancellationToken).ToOk())
            .WithName("GetClient")
            .RequirePermission(Permissions.ClientsRead);

        group.MapPut(
                "/{id:guid}",
                (Guid id, UpdateClientRequest request, ICommandHandler<UpdateClientCommand, ClientDetailResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateClientCommand(http.User.ToViewer(), id, request.Goals, request.TrainerNotes), cancellationToken).ToOk())
            .WithName("UpdateClient")
            .RequirePermission(Permissions.ClientsManage);

        group.MapPost(
                "/{id:guid}/archive",
                (Guid id, ICommandHandler<ArchiveClientCommand, ClientDetailResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ArchiveClientCommand(http.User.GetUserId(), id), cancellationToken).ToOk())
            .WithName("ArchiveClient")
            .RequirePermission(Permissions.ClientsManage);

        group.MapPost(
                "/{id:guid}/unarchive",
                (Guid id, ICommandHandler<UnarchiveClientCommand, ClientDetailResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UnarchiveClientCommand(http.User.GetUserId(), id), cancellationToken).ToOk())
            .WithName("UnarchiveClient")
            .RequirePermission(Permissions.ClientsManage);
    }
}
