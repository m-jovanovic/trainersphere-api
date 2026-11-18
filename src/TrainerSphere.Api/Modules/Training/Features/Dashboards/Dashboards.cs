using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Dashboards;

internal sealed record TodayItemResponse(Guid? AssignmentId, ClientRef Client, string WorkoutName, AssignmentStatus Status, Guid? SessionId, DateTime? CompletedAt);

internal sealed record ClientAdherenceResponse(
    ClientRef Client,
    decimal? Adherence7d,
    decimal? Adherence28d,
    int Due7d,
    int Completed7d,
    int Due28d,
    int Completed28d,
    DateTime? LastSessionAt);

internal sealed record DashboardInvitationResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    string? Message,
    string Status,
    DateTime CreatedAt,
    DateTime ExpiresAt,
    DateTime? AcceptedAt,
    Guid? ClientId);

internal sealed record PendingInvitationsResponse(int Count, IReadOnlyList<DashboardInvitationResponse> Items);

internal sealed record TrainerDashboardResponse(
    DateOnly Date,
    int ActiveClients,
    IReadOnlyList<TodayItemResponse> Today,
    IReadOnlyList<ClientAdherenceResponse> Adherence,
    IReadOnlyList<ClientPersonalRecordResponse> RecentPersonalRecords,
    PendingInvitationsResponse PendingInvitations,
    IReadOnlyList<ClientSessionSummaryResponse> RecentSessions);

internal sealed record ThisWeekResponse(int Due, int Completed, decimal? Rate);

internal sealed record ClientDashboardResponse(
    DateOnly Date,
    IReadOnlyList<AssignmentResponse> Today,
    AssignmentResponse? NextAssignment,
    SessionSummaryResponse? InProgressSession,
    ThisWeekResponse ThisWeek,
    decimal? Adherence7d,
    decimal? Adherence28d,
    int StreakWeeks,
    IReadOnlyList<PersonalRecordResponse> RecentPersonalRecords,
    IReadOnlyList<SessionSummaryResponse> RecentSessions,
    BodyMetricResponse? LatestBodyMetric);

internal sealed record TrainerDashboardQuery(Viewer Viewer) : IQuery<TrainerDashboardResponse>;

internal sealed class TrainerDashboardHandler(
    IClientRepository clients,
    IAssignmentRepository assignments,
    IExerciseRepository exercises,
    IInvitationProjection invitations,
    IDbSession db,
    TimeProvider timeProvider) : IQueryHandler<TrainerDashboardQuery, TrainerDashboardResponse>
{
    public const int RecentPersonalRecordDays = 14;
    public const int RecentPersonalRecordLimit = 20;
    public const int PendingInvitationLimit = 10;
    public const int RecentSessionLimit = 10;

    public async Task<Result<TrainerDashboardResponse>> Handle(TrainerDashboardQuery query, CancellationToken cancellationToken)
    {
        Guid trainerId = query.Viewer.UserId;
        DateTime now = timeProvider.UtcNow();
        DateOnly today = timeProvider.Today(await clients.GetTrainerTimeZoneAsync(trainerId, cancellationToken));
        IReadOnlyList<ClientProfile> roster = await clients.ListAsync(trainerId, search: null, status: null, cancellationToken);
        var actives = roster.Where(c => !c.IsArchived).ToList();
        var byId = actives.ToDictionary(c => c.Id);

        List<TodayItemResponse> todayItems = await GetTodayItemsAsync(trainerId, byId, now, cancellationToken);
        IReadOnlyDictionary<Guid, ClientStats> stats = await ClientStatsReader.ReadAsync(db, assignments, trainerId, actives, timeProvider, cancellationToken);

        var adherence = actives
            .Select(c => (Client: c, Stats: stats[c.Id]))
            .OrderBy(x => x.Stats.Adherence7d.Rate is null)
            .ThenBy(x => x.Stats.Adherence7d.Rate)
            .ThenBy(x => x.Stats.Adherence28d.Rate is null)
            .ThenBy(x => x.Stats.Adherence28d.Rate)
            .ThenBy(x => x.Client.FirstName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Client.LastName, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ClientAdherenceResponse(
                ClientRef.From(x.Client),
                x.Stats.Adherence7d.Rate,
                x.Stats.Adherence28d.Rate,
                x.Stats.Adherence7d.Due,
                x.Stats.Adherence7d.Completed,
                x.Stats.Adherence28d.Due,
                x.Stats.Adherence28d.Completed,
                x.Stats.LastSessionAt))
            .ToList();

        IReadOnlyList<ClientPersonalRecordResponse> recentRecords = await GetRecentPersonalRecordsAsync(trainerId, now, cancellationToken);
        PendingInvitationsResponse pendingInvitations = await GetPendingInvitationsAsync(trainerId, now, cancellationToken);

        IReadOnlyList<SessionSummaryRow> recent = await db.QueryAsync<SessionSummaryRow>(
            $"{SessionSummarySql.Select} WHERE s.trainer_id = @TrainerId ORDER BY s.started_at DESC, s.id DESC LIMIT {RecentSessionLimit}",
            new { TrainerId = trainerId },
            cancellationToken);

        return new TrainerDashboardResponse(
            today,
            actives.Count,
            todayItems,
            adherence,
            recentRecords,
            pendingInvitations,
            [.. recent.Select(r => r.ToClientSummary())]);
    }

    private async Task<List<TodayItemResponse>> GetTodayItemsAsync(Guid trainerId, Dictionary<Guid, ClientProfile> actives, DateTime now, CancellationToken cancellationToken)
    {
        if (actives.Count == 0)
        {
            return [];
        }

        var todays = actives.ToDictionary(pair => pair.Key, pair => timeProvider.Today(pair.Value.TimeZone));
        IReadOnlyList<Assignment> dated = await assignments.ListAsync(trainerId, clientId: null, todays.Values.Min(), todays.Values.Max(), cancellationToken);

        var items = dated
            .Where(a => actives.ContainsKey(a.ClientId) && a.Date == todays[a.ClientId])
            .Select(a => new TodayItemResponse(a.Id, ClientRef.From(actives[a.ClientId]), a.Workout.Name, a.StatusOn(todays[a.ClientId]), a.SessionId, a.CompletedAt))
            .ToList();

        // Ad-hoc sessions have no assignment; a two day look-back covers every possible "today" across time zones.
        IReadOnlyList<SessionSummaryRow> adhoc = await db.QueryAsync<SessionSummaryRow>(
            $"{SessionSummarySql.Select} WHERE s.trainer_id = @TrainerId AND s.assignment_id IS NULL AND s.status = 'Completed' AND s.completed_at >= @Since",
            new { TrainerId = trainerId, Since = now.AddDays(-2) },
            cancellationToken);

        items.AddRange(
            adhoc
                .Where(s => s.CompletedAt is { } completedAt
                    && actives.TryGetValue(s.ClientId, out ClientProfile? client)
                    && LocalDates.Of(completedAt, TimeZones.FindOrUtc(client.TimeZone)) == todays[s.ClientId])
                .Select(s => new TodayItemResponse(null, ClientRef.From(actives[s.ClientId]), s.WorkoutName, AssignmentStatus.Completed, s.Id, s.CompletedAt)));

        return
        [
            .. items
                .OrderBy(i => i.Client.FirstName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.Client.LastName, StringComparer.OrdinalIgnoreCase)
                .ThenBy(i => i.WorkoutName, StringComparer.OrdinalIgnoreCase),
        ];
    }

    private async Task<IReadOnlyList<ClientPersonalRecordResponse>> GetRecentPersonalRecordsAsync(Guid trainerId, DateTime now, CancellationToken cancellationToken)
    {
        IReadOnlyList<PersonalRecordRow> rows = await db.QueryAsync<PersonalRecordRow>(
            $"""
            SELECT {SessionRepository.PersonalRecordColumns} FROM training.personal_records
            WHERE trainer_id = @TrainerId AND achieved_at >= @Since
            ORDER BY achieved_at DESC, id DESC
            LIMIT {RecentPersonalRecordLimit}
            """,
            new { TrainerId = trainerId, Since = now.AddDays(-RecentPersonalRecordDays) },
            cancellationToken);

        if (rows.Count == 0)
        {
            return [];
        }

        var records = rows.Select(r => r.ToDomain()).ToList();
        var owners = (await clients.GetManyAsync([.. records.Select(r => r.ClientId).Distinct()], cancellationToken)).ToDictionary(c => c.Id);
        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(records.Select(r => r.ExerciseId), cancellationToken);

        return
        [
            .. records.Select(r => new ClientPersonalRecordResponse(
                r.Id,
                r.ClientId,
                ClientRef.From(owners[r.ClientId]),
                r.ExerciseId,
                summaries.GetValueOrDefault(r.ExerciseId) ?? new ExerciseSummary(r.ExerciseId, null, string.Empty, null),
                r.Type,
                r.Value,
                r.PreviousValue,
                r.WeightKg,
                r.Reps,
                r.SessionId,
                r.AchievedAt)),
        ];
    }

    private async Task<PendingInvitationsResponse> GetPendingInvitationsAsync(Guid trainerId, DateTime now, CancellationToken cancellationToken)
    {
        (int count, IReadOnlyList<PendingInvitation> pending) = await invitations.GetPendingAsync(trainerId, now, PendingInvitationLimit, cancellationToken);

        return new PendingInvitationsResponse(
            count,
            [.. pending.Select(i => new DashboardInvitationResponse(i.Id, i.Email, i.FirstName, i.LastName, i.Message, "pending", i.CreatedAt, i.ExpiresAt, AcceptedAt: null, ClientId: null))]);
    }
}

internal sealed record ClientDashboardQuery(Viewer Viewer) : IQuery<ClientDashboardResponse>;

internal sealed class ClientDashboardHandler(
    IClientRepository clients,
    IAssignmentRepository assignments,
    ISessionRepository sessions,
    IBodyMetricRepository metrics,
    IExerciseRepository exercises,
    IDbSession db,
    TimeProvider timeProvider) : IQueryHandler<ClientDashboardQuery, ClientDashboardResponse>
{
    public const int RecentSessionLimit = 5;

    public Task<Result<ClientDashboardResponse>> Handle(ClientDashboardQuery query, CancellationToken cancellationToken) =>
        clients.GetAsync(query.Viewer.UserId, cancellationToken)
            .ToResult(ClientErrors.NotFound(query.Viewer.UserId))
            .Bind(client => LoadAsync(client, cancellationToken));

    private async Task<Result<ClientDashboardResponse>> LoadAsync(ClientProfile client, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = TimeZones.FindOrUtc(client.TimeZone);
        DateOnly today = timeProvider.Today(client.TimeZone);
        DateTime now = timeProvider.UtcNow();

        DateOnly weekStart = IsoWeek.Start(today);
        DateOnly from = weekStart < today.AddDays(-27) ? weekStart : today.AddDays(-27);
        IReadOnlyList<Assignment> range = await assignments.ListAsync(trainerId: null, client.Id, from, today.AddDays(TrainingRules.MaxAssignmentRangeDays), cancellationToken);

        var todayItems = range.Where(a => a.Date == today).Select(a => AssignmentResponse.From(a, client, today)).ToList();
        AssignmentResponse? next = range
            .Where(a => a.Date > today && a.StoredStatus == AssignmentStatus.Scheduled)
            .Select(a => AssignmentResponse.From(a, client, today))
            .FirstOrDefault();

        ScheduledItem[] items = [.. range.Select(a => new ScheduledItem(a.Date, a.StoredStatus))];
        AdherenceStats thisWeek = Adherence.ThisWeek(items, today);

        SessionSummaryRow? inProgress = await db.QuerySingleOrDefaultAsync<SessionSummaryRow>(
            $"{SessionSummarySql.Select} WHERE s.client_id = @ClientId AND s.status = 'InProgress' ORDER BY s.started_at DESC, s.id DESC LIMIT 1",
            new { ClientId = client.Id },
            cancellationToken);
        IReadOnlyList<SessionSummaryRow> recent = await db.QueryAsync<SessionSummaryRow>(
            $"{SessionSummarySql.Select} WHERE s.client_id = @ClientId ORDER BY s.started_at DESC, s.id DESC LIMIT {RecentSessionLimit}",
            new { ClientId = client.Id },
            cancellationToken);

        IReadOnlyList<PersonalRecordRow> recordRows = await db.QueryAsync<PersonalRecordRow>(
            $"""
            SELECT {SessionRepository.PersonalRecordColumns} FROM training.personal_records
            WHERE client_id = @ClientId AND achieved_at >= @Since
            ORDER BY achieved_at DESC, id DESC
            """,
            new { ClientId = client.Id, Since = now.AddDays(-TrainerDashboardHandler.RecentPersonalRecordDays) },
            cancellationToken);
        IReadOnlyList<PersonalRecordResponse> records = await ProgressViews.ToResponsesAsync([.. recordRows.Select(r => r.ToDomain())], exercises, cancellationToken);

        IReadOnlyList<SessionVolume> completed = await sessions.GetCompletedAsync(client.Id, cancellationToken);
        BodyMetric? latestMetric = await metrics.GetLatestAsync(client.Id, cancellationToken);

        return new ClientDashboardResponse(
            today,
            todayItems,
            next,
            inProgress?.ToSummary(),
            new ThisWeekResponse(thisWeek.Due, thisWeek.Completed, thisWeek.Rate),
            Adherence.LastDays(items, today, 7).Rate,
            Adherence.LastDays(items, today, 28).Rate,
            Streaks.Weeks(completed.Select(s => LocalDates.Of(s.StartedAt, zone)), today),
            records,
            [.. recent.Select(r => r.ToSummary())],
            latestMetric is null ? null : BodyMetricResponse.From(latestMetric));
    }
}

internal sealed class DashboardEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/dashboard").WithTags("Dashboards");

        group.MapGet(
                "/trainer",
                (IQueryHandler<TrainerDashboardQuery, TrainerDashboardResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new TrainerDashboardQuery(http.User.ToViewer()), cancellationToken).ToOk())
            .WithName("GetTrainerDashboard")
            .RequirePermission(Permissions.TrainerDashboard);

        group.MapGet(
                "/client",
                (IQueryHandler<ClientDashboardQuery, ClientDashboardResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ClientDashboardQuery(http.User.ToViewer()), cancellationToken).ToOk())
            .WithName("GetClientDashboard")
            .RequirePermission(Permissions.ClientDashboard);
    }
}
