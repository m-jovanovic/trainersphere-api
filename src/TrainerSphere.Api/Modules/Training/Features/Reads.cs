using TrainerSphere.Api.Modules.Training.Domain;

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
