using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// One workout scheduled for one client on one date. It holds a snapshot of the workout taken when it was
/// scheduled.
/// </summary>
internal sealed class Assignment
{
    public const int NotesMaxLength = 1000;
    public const int ReasonMaxLength = 500;

    private Assignment(AssignmentState state)
    {
        Id = state.Id;
        ClientId = state.ClientId;
        TrainerId = state.TrainerId;
        Date = state.Date;
        StoredStatus = state.Status;
        WorkoutId = state.WorkoutId;
        Workout = state.Workout;
        ProgramAssignmentId = state.ProgramAssignmentId;
        ProgramName = state.ProgramName;
        Notes = state.Notes;
        SessionId = state.SessionId;
        CompletedAt = state.CompletedAt;
        SkippedAt = state.SkippedAt;
        SkipReason = state.SkipReason;
        CreatedAt = state.CreatedAt;
        UpdatedAt = state.UpdatedAt;
    }

    public Guid Id { get; }

    public Guid ClientId { get; }

    public Guid TrainerId { get; }

    public DateOnly Date { get; private set; }

    /// <summary>
    /// Gets the persisted status: scheduled, completed or skipped. See <see cref="StatusOn(DateOnly)"/> for the displayed one.
    /// </summary>
    public AssignmentStatus StoredStatus { get; private set; }

    public Guid WorkoutId { get; }

    public WorkoutSnapshot Workout { get; }

    public Guid? ProgramAssignmentId { get; }

    public string? ProgramName { get; }

    public string? Notes { get; private set; }

    public Guid? SessionId { get; private set; }

    public DateTime? CompletedAt { get; private set; }

    public DateTime? SkippedAt { get; private set; }

    public string? SkipReason { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsScheduled => StoredStatus == AssignmentStatus.Scheduled;

    public static Assignment Schedule(
        Guid id,
        ClientProfile client,
        Guid workoutId,
        WorkoutSnapshot workout,
        DateOnly date,
        string? notes,
        ProgramAssignment? program,
        DateTime now)
    {
        ArgumentNullException.ThrowIfNull(client);

        return new Assignment(new AssignmentState(
            id,
            client.Id,
            client.TrainerId,
            date,
            AssignmentStatus.Scheduled,
            workoutId,
            workout,
            program?.Id,
            program?.ProgramName,
            Text.NullIfBlank(notes),
            SessionId: null,
            CompletedAt: null,
            SkippedAt: null,
            SkipReason: null,
            now,
            now));
    }

    public static Assignment Restore(AssignmentState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        return new Assignment(state);
    }

    /// <summary>
    /// The status shown to users: a scheduled assignment dated before the client's today is missed.
    /// </summary>
    public static AssignmentStatus StatusOn(AssignmentStatus stored, DateOnly date, DateOnly clientToday) =>
        stored == AssignmentStatus.Scheduled && date < clientToday ? AssignmentStatus.Missed : stored;

    public AssignmentStatus StatusOn(DateOnly clientToday) => StatusOn(StoredStatus, Date, clientToday);

    public bool IsAccessibleBy(Viewer viewer) => viewer.IsTrainer ? TrainerId == viewer.UserId : ClientId == viewer.UserId;

    public AssignmentState ToState() =>
        new(Id, ClientId, TrainerId, Date, StoredStatus, WorkoutId, Workout, ProgramAssignmentId, ProgramName, Notes, SessionId, CompletedAt, SkippedAt, SkipReason, CreatedAt, UpdatedAt);

    public Result<Assignment> Reschedule(DateOnly date, string? notes, DateTime now)
    {
        if (!IsScheduled)
        {
            return AssignmentErrors.NotScheduled(StoredStatus);
        }

        Date = date;
        Notes = Text.NullIfBlank(notes);
        UpdatedAt = now;
        return this;
    }

    /// <summary>
    /// Only a scheduled assignment that nobody started can be deleted; anything else is history.
    /// </summary>
    public Result<Assignment> EnsureDeletable() =>
        (IsScheduled, SessionId) switch
        {
            (false, _) => AssignmentErrors.NotScheduled(StoredStatus),
            (true, { } sessionId) => AssignmentErrors.HasSession(sessionId),
            (true, null) => this,
        };

    public Result<Assignment> Skip(string? reason, DateTime now)
    {
        if (EnsureDeletable().Value is Error error)
        {
            return error;
        }

        StoredStatus = AssignmentStatus.Skipped;
        SkippedAt = now;
        SkipReason = Text.NullIfBlank(reason);
        UpdatedAt = now;
        return this;
    }

    public Result<Assignment> StartSession(Guid sessionId, DateTime now)
    {
        if (!IsScheduled)
        {
            return AssignmentErrors.NotScheduled(StoredStatus);
        }

        if (SessionId is { } existing)
        {
            return SessionErrors.AlreadyStartedForAssignment(existing);
        }

        SessionId = sessionId;
        UpdatedAt = now;
        return this;
    }

    public void Complete(DateTime completedAt, DateTime now)
    {
        StoredStatus = AssignmentStatus.Completed;
        CompletedAt = completedAt;
        UpdatedAt = now;
    }

    /// <summary>
    /// Called when the in-progress session started from this assignment is discarded.
    /// </summary>
    public void DetachSession(DateTime now)
    {
        SessionId = null;
        UpdatedAt = now;
    }
}

internal sealed record AssignmentState(
    Guid Id,
    Guid ClientId,
    Guid TrainerId,
    DateOnly Date,
    AssignmentStatus Status,
    Guid WorkoutId,
    WorkoutSnapshot Workout,
    Guid? ProgramAssignmentId,
    string? ProgramName,
    string? Notes,
    Guid? SessionId,
    DateTime? CompletedAt,
    DateTime? SkippedAt,
    string? SkipReason,
    DateTime CreatedAt,
    DateTime UpdatedAt);
