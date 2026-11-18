using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.Api.Modules.Training.Data;

internal interface IAssignmentRepository
{
    /// <summary>
    /// Gets the assignment; with <paramref name="forUpdate"/> the row is locked until the transaction ends.
    /// </summary>
    Task<Assignment?> GetAsync(Guid id, bool forUpdate, CancellationToken cancellationToken);

    /// <summary>
    /// Lists assignments dated from <paramref name="from"/> to <paramref name="to"/> (inclusive) of a trainer, a client, or both, by date.
    /// </summary>
    Task<IReadOnlyList<Assignment>> ListAsync(Guid? trainerId, Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken);

    Task AddAsync(Assignment assignment, CancellationToken cancellationToken);

    Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes the client's scheduled assignments without a session dated on or after <paramref name="fromDate"/>.
    /// </summary>
    Task<int> DeleteScheduledFromAsync(Guid clientId, DateOnly fromDate, CancellationToken cancellationToken);

    /// <summary>
    /// Deletes a program assignment's scheduled assignments without a session dated on or after <paramref name="fromDate"/>.
    /// </summary>
    Task<int> DeleteScheduledForProgramFromAsync(Guid programAssignmentId, DateOnly fromDate, CancellationToken cancellationToken);
}

internal interface ISessionRepository
{
    /// <summary>
    /// Gets the session; with <paramref name="forUpdate"/> the row is locked until the transaction ends, so set
    /// writes and completion of the same session are serialized.
    /// </summary>
    Task<Session?> GetAsync(Guid id, bool forUpdate, CancellationToken cancellationToken);

    Task AddAsync(Session session, CancellationToken cancellationToken);

    Task UpdateAsync(Session session, CancellationToken cancellationToken);

    Task DeleteAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<SessionSet>> GetSetsAsync(Guid sessionId, CancellationToken cancellationToken);

    Task<Guid?> GetSessionIdOfSetAsync(Guid setId, CancellationToken cancellationToken);

    Task UpsertSetAsync(SessionSet set, CancellationToken cancellationToken);

    Task DeleteSetAsync(Guid sessionId, Guid setId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets the completed sets of the client's other completed sessions for the exercises.
    /// </summary>
    Task<IReadOnlyList<LoggedSet>> GetHistoryAsync(Guid clientId, IReadOnlyCollection<Guid> exerciseIds, Guid exceptSessionId, CancellationToken cancellationToken);

    Task AddPersonalRecordsAsync(IReadOnlyCollection<PersonalRecord> records, CancellationToken cancellationToken);

    Task<IReadOnlyList<PersonalRecord>> GetPersonalRecordsAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Gets, per exercise, the completed sets of the client's most recent other completed session that logged it.
    /// </summary>
    Task<IReadOnlyList<PerformedSet>> GetLastPerformanceAsync(Guid clientId, Guid exceptSessionId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken);
}

internal interface IBodyMetricRepository
{
    Task UpsertAsync(BodyMetric metric, CancellationToken cancellationToken);

    Task DeleteAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken);
}

internal sealed record PerformedSet(Guid ExerciseId, Guid SessionId, DateTime StartedAt, int SetNumber, decimal? WeightKg, int? Reps, decimal? Rpe);

internal sealed class AssignmentRepository(IDbSession db) : IAssignmentRepository
{
    public const string Columns =
        "id, client_id, trainer_id, date, status, workout_id, workout::text AS workout, program_assignment_id, program_name, notes, session_id, completed_at, skipped_at, skip_reason, created_at, updated_at";

    public async Task<Assignment?> GetAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        AssignmentRow? row = await db.QuerySingleOrDefaultAsync<AssignmentRow>(
            $"SELECT {Columns} FROM training.assignments WHERE id = @Id{(forUpdate ? " FOR UPDATE" : string.Empty)}",
            new { Id = id },
            cancellationToken);

        return row is null ? null : Assignment.Restore(row.ToState());
    }

    public async Task<IReadOnlyList<Assignment>> ListAsync(Guid? trainerId, Guid? clientId, DateOnly from, DateOnly to, CancellationToken cancellationToken)
    {
        IReadOnlyList<AssignmentRow> rows = await db.QueryAsync<AssignmentRow>(
            $"""
            SELECT {Columns} FROM training.assignments
            WHERE (@TrainerId::uuid IS NULL OR trainer_id = @TrainerId::uuid) AND (@ClientId::uuid IS NULL OR client_id = @ClientId::uuid)
              AND date BETWEEN @From AND @To
            ORDER BY date, created_at, id
            """,
            new { TrainerId = trainerId, ClientId = clientId, From = from, To = to },
            cancellationToken);

        return [.. rows.Select(r => Assignment.Restore(r.ToState()))];
    }

    public Task AddAsync(Assignment assignment, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.assignments (id, client_id, trainer_id, date, status, workout_id, workout, program_assignment_id, program_name, notes, session_id, completed_at, skipped_at, skip_reason, created_at, updated_at)
            VALUES (@Id, @ClientId, @TrainerId, @Date, @Status, @WorkoutId, @Workout::jsonb, @ProgramAssignmentId, @ProgramName, @Notes, @SessionId, @CompletedAt, @SkippedAt, @SkipReason, @CreatedAt, @UpdatedAt)
            """,
            Parameters(assignment),
            cancellationToken);

    public Task UpdateAsync(Assignment assignment, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            UPDATE training.assignments
            SET date = @Date, status = @Status, notes = @Notes, session_id = @SessionId, completed_at = @CompletedAt,
                skipped_at = @SkippedAt, skip_reason = @SkipReason, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            Parameters(assignment),
            cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        db.ExecuteAsync("DELETE FROM training.assignments WHERE id = @Id", new { Id = id }, cancellationToken);

    public Task<int> DeleteScheduledFromAsync(Guid clientId, DateOnly fromDate, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "DELETE FROM training.assignments WHERE client_id = @ClientId AND status = 'Scheduled' AND session_id IS NULL AND date >= @FromDate",
            new { ClientId = clientId, FromDate = fromDate },
            cancellationToken);

    public Task<int> DeleteScheduledForProgramFromAsync(Guid programAssignmentId, DateOnly fromDate, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "DELETE FROM training.assignments WHERE program_assignment_id = @Id AND status = 'Scheduled' AND session_id IS NULL AND date >= @FromDate",
            new { Id = programAssignmentId, FromDate = fromDate },
            cancellationToken);

    private static object Parameters(Assignment a) =>
        new
        {
            a.Id,
            a.ClientId,
            a.TrainerId,
            a.Date,
            Status = a.StoredStatus.ToString(),
            a.WorkoutId,
            Workout = TrainingJson.Serialize(a.Workout),
            a.ProgramAssignmentId,
            a.ProgramName,
            a.Notes,
            a.SessionId,
            a.CompletedAt,
            a.SkippedAt,
            a.SkipReason,
            a.CreatedAt,
            a.UpdatedAt,
        };
}

internal sealed record AssignmentRow
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public Guid TrainerId { get; init; }

    public DateOnly Date { get; init; }

    public string Status { get; init; } = string.Empty;

    public Guid WorkoutId { get; init; }

    public string Workout { get; init; } = string.Empty;

    public Guid? ProgramAssignmentId { get; init; }

    public string? ProgramName { get; init; }

    public string? Notes { get; init; }

    public Guid? SessionId { get; init; }

    public DateTime? CompletedAt { get; init; }

    public DateTime? SkippedAt { get; init; }

    public string? SkipReason { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public AssignmentState ToState() =>
        new(
            Id,
            ClientId,
            TrainerId,
            Date,
            Enum.Parse<AssignmentStatus>(Status),
            WorkoutId,
            TrainingJson.Deserialize<WorkoutSnapshot>(Workout),
            ProgramAssignmentId,
            ProgramName,
            Notes,
            SessionId,
            CompletedAt,
            SkippedAt,
            SkipReason,
            CreatedAt,
            UpdatedAt);
}

internal sealed class SessionRepository(IDbSession db) : ISessionRepository
{
    public const string Columns =
        "id, client_id, trainer_id, assignment_id, workout_name, status, started_at, completed_at, notes, prescription::text AS prescription, created_at, updated_at";

    public const string PersonalRecordColumns =
        "id, client_id, trainer_id, exercise_id, type, value, previous_value, weight_kg, reps, session_id, achieved_at";

    public const string SetColumns =
        "id, session_id, exercise_id, position, set_number, weight_kg, reps, duration_seconds, distance_meters, rpe, completed, notes, updated_at";

    public async Task<Session?> GetAsync(Guid id, bool forUpdate, CancellationToken cancellationToken)
    {
        SessionRow? row = await db.QuerySingleOrDefaultAsync<SessionRow>(
            $"SELECT {Columns} FROM training.sessions WHERE id = @Id{(forUpdate ? " FOR UPDATE" : string.Empty)}",
            new { Id = id },
            cancellationToken);

        return row is null ? null : Session.Restore(row.ToState());
    }

    public Task AddAsync(Session session, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.sessions (id, client_id, trainer_id, assignment_id, workout_name, status, started_at, completed_at, notes, prescription, created_at, updated_at)
            VALUES (@Id, @ClientId, @TrainerId, @AssignmentId, @WorkoutName, @Status, @StartedAt, @CompletedAt, @Notes, @Prescription::jsonb, @CreatedAt, @UpdatedAt)
            """,
            new
            {
                session.Id,
                session.ClientId,
                session.TrainerId,
                session.AssignmentId,
                session.WorkoutName,
                Status = session.Status.ToString(),
                session.StartedAt,
                session.CompletedAt,
                session.Notes,
                Prescription = TrainingJson.Serialize(session.Prescription),
                session.CreatedAt,
                session.UpdatedAt,
            },
            cancellationToken);

    public Task UpdateAsync(Session session, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            UPDATE training.sessions
            SET workout_name = @WorkoutName, status = @Status, completed_at = @CompletedAt, notes = @Notes, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new { session.Id, session.WorkoutName, Status = session.Status.ToString(), session.CompletedAt, session.Notes, session.UpdatedAt },
            cancellationToken);

    public Task DeleteAsync(Guid id, CancellationToken cancellationToken) =>
        db.ExecuteAsync("DELETE FROM training.sessions WHERE id = @Id", new { Id = id }, cancellationToken);

    public async Task<IReadOnlyList<SessionSet>> GetSetsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<SessionSetRow> rows = await db.QueryAsync<SessionSetRow>(
            $"SELECT {SetColumns} FROM training.session_sets WHERE session_id = @SessionId ORDER BY position, set_number, updated_at",
            new { SessionId = sessionId },
            cancellationToken);

        return [.. rows.Select(r => r.ToDomain())];
    }

    public Task<Guid?> GetSessionIdOfSetAsync(Guid setId, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<Guid?>("SELECT session_id FROM training.session_sets WHERE id = @Id", new { Id = setId }, cancellationToken);

    public Task UpsertSetAsync(SessionSet set, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.session_sets (id, session_id, exercise_id, position, set_number, weight_kg, reps, duration_seconds, distance_meters, rpe, completed, notes, updated_at)
            VALUES (@Id, @SessionId, @ExerciseId, @Position, @SetNumber, @WeightKg, @Reps, @DurationSeconds, @DistanceMeters, @Rpe, @Completed, @Notes, @UpdatedAt)
            ON CONFLICT (id) DO UPDATE
            SET exercise_id = EXCLUDED.exercise_id, position = EXCLUDED.position, set_number = EXCLUDED.set_number, weight_kg = EXCLUDED.weight_kg,
                reps = EXCLUDED.reps, duration_seconds = EXCLUDED.duration_seconds, distance_meters = EXCLUDED.distance_meters, rpe = EXCLUDED.rpe,
                completed = EXCLUDED.completed, notes = EXCLUDED.notes, updated_at = EXCLUDED.updated_at
            WHERE session_sets.session_id = EXCLUDED.session_id
            """,
            set,
            cancellationToken);

    public Task DeleteSetAsync(Guid sessionId, Guid setId, CancellationToken cancellationToken) =>
        db.ExecuteAsync("DELETE FROM training.session_sets WHERE id = @Id AND session_id = @SessionId", new { Id = setId, SessionId = sessionId }, cancellationToken);

    public async Task<IReadOnlyList<LoggedSet>> GetHistoryAsync(Guid clientId, IReadOnlyCollection<Guid> exerciseIds, Guid exceptSessionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<LoggedSetRow> rows = await db.QueryAsync<LoggedSetRow>(
            """
            SELECT ss.session_id, ss.exercise_id, ss.weight_kg, ss.reps
            FROM training.session_sets ss
            JOIN training.sessions s ON s.id = ss.session_id
            WHERE s.client_id = @ClientId AND s.status = 'Completed' AND s.id <> @ExceptSessionId AND ss.completed AND ss.exercise_id = ANY(@ExerciseIds)
            """,
            new { ClientId = clientId, ExerciseIds = exerciseIds.Distinct().ToArray(), ExceptSessionId = exceptSessionId },
            cancellationToken);

        return [.. rows.Select(r => new LoggedSet(r.SessionId, r.ExerciseId, r.WeightKg, r.Reps))];
    }

    public async Task AddPersonalRecordsAsync(IReadOnlyCollection<PersonalRecord> records, CancellationToken cancellationToken)
    {
        if (records.Count == 0)
        {
            return;
        }

        await db.ExecuteAsync(
            """
            INSERT INTO training.personal_records (id, client_id, trainer_id, exercise_id, type, value, previous_value, weight_kg, reps, session_id, achieved_at)
            VALUES (@Id, @ClientId, @TrainerId, @ExerciseId, @Type, @Value, @PreviousValue, @WeightKg, @Reps, @SessionId, @AchievedAt)
            ON CONFLICT (session_id, exercise_id, type) DO NOTHING
            """,
            records.Select(r => new
            {
                r.Id,
                r.ClientId,
                r.TrainerId,
                r.ExerciseId,
                Type = r.Type.ToString(),
                r.Value,
                r.PreviousValue,
                r.WeightKg,
                r.Reps,
                r.SessionId,
                r.AchievedAt,
            }).ToList(),
            cancellationToken);
    }

    public async Task<IReadOnlyList<PersonalRecord>> GetPersonalRecordsAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        IReadOnlyList<PersonalRecordRow> rows = await db.QueryAsync<PersonalRecordRow>(
            $"SELECT {PersonalRecordColumns} FROM training.personal_records WHERE session_id = @SessionId ORDER BY exercise_id, type",
            new { SessionId = sessionId },
            cancellationToken);

        return [.. rows.Select(r => r.ToDomain())];
    }

    public async Task<IReadOnlyList<PerformedSet>> GetLastPerformanceAsync(Guid clientId, Guid exceptSessionId, IReadOnlyCollection<Guid> exerciseIds, CancellationToken cancellationToken)
    {
        if (exerciseIds.Count == 0)
        {
            return [];
        }

        IReadOnlyList<PerformedSetRow> rows = await db.QueryAsync<PerformedSetRow>(
            """
            WITH latest AS (
                SELECT DISTINCT ON (ss.exercise_id) ss.exercise_id, s.id AS session_id, s.started_at
                FROM training.session_sets ss
                JOIN training.sessions s ON s.id = ss.session_id
                WHERE s.client_id = @ClientId AND s.status = 'Completed' AND s.id <> @ExceptSessionId AND ss.completed AND ss.exercise_id = ANY(@ExerciseIds)
                ORDER BY ss.exercise_id, s.completed_at DESC, s.id DESC)
            SELECT l.exercise_id, l.session_id, l.started_at, ss.set_number, ss.weight_kg, ss.reps, ss.rpe
            FROM latest l
            JOIN training.session_sets ss ON ss.session_id = l.session_id AND ss.exercise_id = l.exercise_id AND ss.completed
            ORDER BY l.exercise_id, ss.set_number, ss.position
            """,
            new { ClientId = clientId, ExceptSessionId = exceptSessionId, ExerciseIds = exerciseIds.Distinct().ToArray() },
            cancellationToken);

        return [.. rows.Select(r => new PerformedSet(r.ExerciseId, r.SessionId, r.StartedAt, r.SetNumber, r.WeightKg, r.Reps, r.Rpe))];
    }
}

internal sealed record PersonalRecordRow
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public Guid TrainerId { get; init; }

    public Guid ExerciseId { get; init; }

    public string Type { get; init; } = string.Empty;

    public decimal Value { get; init; }

    public decimal PreviousValue { get; init; }

    public decimal? WeightKg { get; init; }

    public int? Reps { get; init; }

    public Guid SessionId { get; init; }

    public DateTime AchievedAt { get; init; }

    public PersonalRecord ToDomain() =>
        new(Id, ClientId, TrainerId, ExerciseId, Enum.Parse<PersonalRecordType>(Type), Value, PreviousValue, WeightKg, Reps, SessionId, AchievedAt);
}

internal sealed record PerformedSetRow
{
    public Guid ExerciseId { get; init; }

    public Guid SessionId { get; init; }

    public DateTime StartedAt { get; init; }

    public int SetNumber { get; init; }

    public decimal? WeightKg { get; init; }

    public int? Reps { get; init; }

    public decimal? Rpe { get; init; }
}

internal sealed record SessionRow
{
    public Guid Id { get; init; }

    public Guid ClientId { get; init; }

    public Guid TrainerId { get; init; }

    public Guid? AssignmentId { get; init; }

    public string WorkoutName { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime StartedAt { get; init; }

    public DateTime? CompletedAt { get; init; }

    public string? Notes { get; init; }

    public string Prescription { get; init; } = "[]";

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }

    public SessionState ToState() =>
        new(
            Id,
            ClientId,
            TrainerId,
            AssignmentId,
            WorkoutName,
            Enum.Parse<SessionStatus>(Status),
            StartedAt,
            CompletedAt,
            Notes,
            TrainingJson.Deserialize<WorkoutExerciseView[]>(Prescription),
            CreatedAt,
            UpdatedAt);
}

internal sealed record LoggedSetRow
{
    public Guid SessionId { get; init; }

    public Guid ExerciseId { get; init; }

    public decimal? WeightKg { get; init; }

    public int? Reps { get; init; }
}

internal sealed record SessionSetRow
{
    public Guid Id { get; init; }

    public Guid SessionId { get; init; }

    public Guid ExerciseId { get; init; }

    public int Position { get; init; }

    public int SetNumber { get; init; }

    public decimal? WeightKg { get; init; }

    public int? Reps { get; init; }

    public int? DurationSeconds { get; init; }

    public decimal? DistanceMeters { get; init; }

    public decimal? Rpe { get; init; }

    public bool Completed { get; init; }

    public string? Notes { get; init; }

    public DateTime UpdatedAt { get; init; }

    public SessionSet ToDomain() =>
        new(Id, SessionId, ExerciseId, Position, SetNumber, WeightKg, Reps, DurationSeconds, DistanceMeters, Rpe, Completed, Notes, UpdatedAt);
}

internal sealed class BodyMetricRepository(IDbSession db) : IBodyMetricRepository
{
    public Task UpsertAsync(BodyMetric metric, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.body_metrics (client_id, date, weight_kg, body_fat_percent, waist_cm, chest_cm, hips_cm, arm_cm, thigh_cm, resting_heart_rate, notes, recorded_by, updated_at)
            VALUES (@ClientId, @Date, @WeightKg, @BodyFatPercent, @WaistCm, @ChestCm, @HipsCm, @ArmCm, @ThighCm, @RestingHeartRate, @Notes, @RecordedBy, @UpdatedAt)
            ON CONFLICT (client_id, date) DO UPDATE
            SET weight_kg = EXCLUDED.weight_kg, body_fat_percent = EXCLUDED.body_fat_percent, waist_cm = EXCLUDED.waist_cm, chest_cm = EXCLUDED.chest_cm,
                hips_cm = EXCLUDED.hips_cm, arm_cm = EXCLUDED.arm_cm, thigh_cm = EXCLUDED.thigh_cm, resting_heart_rate = EXCLUDED.resting_heart_rate,
                notes = EXCLUDED.notes, recorded_by = EXCLUDED.recorded_by, updated_at = EXCLUDED.updated_at
            """,
            new
            {
                metric.ClientId,
                metric.Date,
                metric.WeightKg,
                metric.BodyFatPercent,
                metric.WaistCm,
                metric.ChestCm,
                metric.HipsCm,
                metric.ArmCm,
                metric.ThighCm,
                metric.RestingHeartRate,
                metric.Notes,
                RecordedBy = metric.RecordedBy.ToString(),
                metric.UpdatedAt,
            },
            cancellationToken);

    public Task DeleteAsync(Guid clientId, DateOnly date, CancellationToken cancellationToken) =>
        db.ExecuteAsync("DELETE FROM training.body_metrics WHERE client_id = @ClientId AND date = @Date", new { ClientId = clientId, Date = date }, cancellationToken);
}
