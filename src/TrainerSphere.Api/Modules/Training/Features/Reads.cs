using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Training.Features;

/// <summary>
/// The session summary projection shared by the session list and the dashboards. Callers append the WHERE,
/// ORDER BY and LIMIT clauses; the sessions table is aliased <c>s</c> and the clients table <c>c</c>.
/// </summary>
internal static class SessionSummarySql
{
    public const string Select =
        """
        SELECT s.id, s.client_id, s.assignment_id, s.workout_name, s.status, s.started_at, s.completed_at,
               COALESCE((SELECT sum(ss.weight_kg * ss.reps) FROM training.session_sets ss WHERE ss.session_id = s.id AND ss.completed), 0) AS total_volume_kg,
               (SELECT count(*)::int FROM training.session_sets ss WHERE ss.session_id = s.id AND ss.completed) AS completed_sets,
               (SELECT count(DISTINCT pr.exercise_id)::int FROM training.personal_records pr WHERE pr.session_id = s.id) AS personal_record_count,
               c.first_name, c.last_name
        FROM training.sessions s
        JOIN training.clients c ON c.id = s.client_id
        """;
}

internal sealed record SessionSummaryRow
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public Guid? AssignmentId { get; init; }

    public string WorkoutName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public decimal TotalVolumeKg { get; init; }

    public int CompletedSets { get; init; }

    public int PersonalRecordCount { get; init; }

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public SessionSummaryResponse ToSummary() =>
        new(Id, ClientId, AssignmentId, WorkoutName, Enum.Parse<SessionStatus>(Status), StartedAt, CompletedAt, TotalVolumeKg, CompletedSets, PersonalRecordCount);

    public ClientSessionSummaryResponse ToClientSummary() =>
        new(
            Id,
            ClientId,
            new ClientRef(ClientId, FirstName, LastName),
            AssignmentId,
            WorkoutName,
            Enum.Parse<SessionStatus>(Status),
            StartedAt,
            CompletedAt,
            TotalVolumeKg,
            CompletedSets,
            PersonalRecordCount);
}

internal sealed record ClientStats(DateTime? LastSessionAt, AdherenceStats Adherence7d, AdherenceStats Adherence28d, NextAssignmentRef? NextAssignment);

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

        // On or after the client's today, unlike the client dashboard's strictly-after; same query, different bound.
        IReadOnlyDictionary<Guid, Assignment> nextByClient = await assignments.GetNextScheduledAsync([.. clients.Select(c => (c.Id, todays[c.Id].AddDays(-1)))], cancellationToken);

        IReadOnlyList<Assignment> window = await assignments.ListAsync(trainerId, clientId: null, minToday.AddDays(-27), maxToday, cancellationToken);
        var itemsByClient = window
            .GroupBy(a => a.ClientId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<ScheduledItem>)[.. g.Select(a => new ScheduledItem(a.Date, a.StoredStatus))]);
        var lastSessionByClient = lastSessions.ToDictionary(r => r.ClientId, r => (DateTime?)r.LastSessionAt);

        return clients.ToDictionary(
            c => c.Id,
            c =>
            {
                DateOnly today = todays[c.Id];
                IReadOnlyList<ScheduledItem> items = itemsByClient.GetValueOrDefault(c.Id, []);
                NextAssignmentRef? next = nextByClient.TryGetValue(c.Id, out Assignment? a) ? new NextAssignmentRef(a.Id, a.Date, a.Workout.Name) : null;

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
}

internal static class ProgressViews
{
    public static async Task<IReadOnlyList<PersonalRecordResponse>> ToResponsesAsync(IReadOnlyList<PersonalRecord> records, IExerciseRepository exercises, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(records.Select(r => r.ExerciseId), cancellationToken);

        return [.. records.Select(r => PersonalRecordResponse.From(r, summaries.GetValueOrDefault(r.ExerciseId) ?? new ExerciseSummary(r.ExerciseId, null, string.Empty, null)))];
    }
}
