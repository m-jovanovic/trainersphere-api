using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.Api.Modules.Training.Features;

internal sealed record ClientRef(Guid Id, string FirstName, string LastName)
{
    public static ClientRef From(ClientProfile client) => new(client.Id, client.FirstName, client.LastName);
}

internal sealed record ExerciseResponse(
    Guid Id,
    string? Slug,
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment,
    bool Unilateral,
    string? Cue,
    string? Illustration,
    bool IsCustom,
    bool Archived)
{
    public static ExerciseResponse From(Exercise exercise) =>
        new(
            exercise.Id,
            exercise.Slug,
            exercise.Name,
            exercise.Category,
            exercise.MovementPattern,
            exercise.PrimaryMuscles,
            exercise.SecondaryMuscles,
            exercise.Equipment,
            exercise.Unilateral,
            exercise.Cue,
            exercise.Illustration,
            exercise.IsCustom,
            exercise.Archived);
}

internal sealed record WorkoutResponse(
    Guid Id,
    string Name,
    string? Description,
    bool Archived,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<WorkoutExerciseView> Exercises)
{
    public static WorkoutResponse From(Workout workout, IReadOnlyDictionary<Guid, ExerciseSummary> exercises) =>
        new(
            workout.Id,
            workout.Name,
            workout.Description,
            workout.Archived,
            workout.CreatedAt,
            workout.UpdatedAt,
            [.. workout.Exercises.Select(line => Workout.View(line, exercises))]);
}

internal sealed record WorkoutRef(Guid Id, string Name);

internal sealed record ProgramSlotResponse(Guid Id, int Week, int Day, Guid WorkoutId, WorkoutRef Workout);

internal sealed record ProgramResponse(
    Guid Id,
    string Name,
    string? Description,
    int Weeks,
    bool Archived,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    IReadOnlyList<ProgramSlotResponse> Slots)
{
    public static ProgramResponse From(TrainingProgram program, IReadOnlyDictionary<Guid, string> workoutNames) =>
        new(
            program.Id,
            program.Name,
            program.Description,
            program.Weeks,
            program.Archived,
            program.CreatedAt,
            program.UpdatedAt,
            [
                .. program.Slots.Select(s => new ProgramSlotResponse(
                    s.Id,
                    s.Week,
                    s.Day,
                    s.WorkoutId,
                    new WorkoutRef(s.WorkoutId, workoutNames.GetValueOrDefault(s.WorkoutId, string.Empty)))),
            ]);
}

internal sealed record ProgramAssignmentResponse(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid ClientId,
    DateOnly StartDate,
    DateOnly EndDate,
    int AssignmentCount,
    DateTime CreatedAt);

internal sealed record AssignmentResponse(
    Guid Id,
    Guid ClientId,
    ClientRef Client,
    DateOnly Date,
    AssignmentStatus Status,
    Guid WorkoutId,
    WorkoutSnapshot Workout,
    Guid? ProgramAssignmentId,
    string? ProgramName,
    string? Notes,
    Guid? SessionId,
    DateTime? CompletedAt,
    DateTime CreatedAt)
{
    public static AssignmentResponse From(Assignment assignment, ClientProfile client, DateOnly clientToday) =>
        new(
            assignment.Id,
            assignment.ClientId,
            ClientRef.From(client),
            assignment.Date,
            assignment.StatusOn(clientToday),
            assignment.WorkoutId,
            assignment.Workout,
            assignment.ProgramAssignmentId,
            assignment.ProgramName,
            assignment.Notes,
            assignment.SessionId,
            assignment.CompletedAt,
            assignment.CreatedAt);
}

internal sealed record PrescriptionResponse(int Sets, Goal Goal, Load Load, int RestSeconds, string? Tempo, string? SupersetGroup, string? Notes)
{
    public static PrescriptionResponse From(WorkoutExerciseView line) =>
        new(line.Sets, line.Goal, line.Load, line.RestSeconds, line.Tempo, line.SupersetGroup, line.Notes);
}

internal sealed record LastPerformanceSet(int SetNumber, decimal? WeightKg, int? Reps, decimal? Rpe);

internal sealed record LastPerformance(Guid SessionId, DateOnly Date, IReadOnlyList<LastPerformanceSet> Sets);

internal sealed record SetResponse(
    Guid Id,
    Guid ExerciseId,
    int Position,
    int SetNumber,
    decimal? WeightKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters,
    decimal? Rpe,
    bool Completed,
    string? Notes,
    DateTime UpdatedAt)
{
    public static SetResponse From(SessionSet set) =>
        new(set.Id, set.ExerciseId, set.Position, set.SetNumber, set.WeightKg, set.Reps, set.DurationSeconds, set.DistanceMeters, set.Rpe, set.Completed, set.Notes, set.UpdatedAt);
}

internal sealed record SessionExerciseResponse(
    int Position,
    Guid ExerciseId,
    ExerciseSummary Exercise,
    PrescriptionResponse? Prescription,
    LastPerformance? LastPerformance,
    IReadOnlyList<SetResponse> Sets);

internal sealed record PersonalRecordResponse(
    Guid Id,
    Guid ClientId,
    Guid ExerciseId,
    ExerciseSummary Exercise,
    PersonalRecordType Type,
    decimal Value,
    decimal PreviousValue,
    decimal? WeightKg,
    int? Reps,
    Guid SessionId,
    DateTime AchievedAt)
{
    public static PersonalRecordResponse From(PersonalRecord record, ExerciseSummary exercise) =>
        new(record.Id, record.ClientId, record.ExerciseId, exercise, record.Type, record.Value, record.PreviousValue, record.WeightKg, record.Reps, record.SessionId, record.AchievedAt);
}

internal sealed record ClientPersonalRecordResponse(
    Guid Id,
    Guid ClientId,
    ClientRef Client,
    Guid ExerciseId,
    ExerciseSummary Exercise,
    PersonalRecordType Type,
    decimal Value,
    decimal PreviousValue,
    decimal? WeightKg,
    int? Reps,
    Guid SessionId,
    DateTime AchievedAt);

internal sealed record SessionTotalsResponse(
    decimal TotalVolumeKg,
    int CompletedSets,
    int TotalReps,
    int? DurationSeconds,
    IReadOnlyList<PersonalRecordResponse> PersonalRecords);

internal sealed record SessionResponse(
    Guid Id,
    Guid ClientId,
    Guid? AssignmentId,
    string WorkoutName,
    SessionStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    string? Notes,
    IReadOnlyList<SessionExerciseResponse> Exercises,
    SessionTotalsResponse Summary);

internal sealed record SessionSummaryResponse(
    Guid Id,
    Guid ClientId,
    Guid? AssignmentId,
    string WorkoutName,
    SessionStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    decimal TotalVolumeKg,
    int CompletedSets,
    int PersonalRecordCount);

internal sealed record ClientSessionSummaryResponse(
    Guid Id,
    Guid ClientId,
    ClientRef Client,
    Guid? AssignmentId,
    string WorkoutName,
    SessionStatus Status,
    DateTime StartedAt,
    DateTime? CompletedAt,
    decimal TotalVolumeKg,
    int CompletedSets,
    int PersonalRecordCount);

internal sealed record BodyMetricResponse(
    Guid ClientId,
    DateOnly Date,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes,
    RecordedBy RecordedBy,
    DateTime UpdatedAt)
{
    public static BodyMetricResponse From(BodyMetric m) =>
        new(m.ClientId, m.Date, m.WeightKg, m.BodyFatPercent, m.WaistCm, m.ChestCm, m.HipsCm, m.ArmCm, m.ThighCm, m.RestingHeartRate, m.Notes, m.RecordedBy, m.UpdatedAt);
}

internal sealed record NextAssignmentRef(Guid Id, DateOnly Date, string WorkoutName);

internal sealed record ClientSummaryResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    ClientStatus Status,
    DateTime JoinedAt,
    DateTime? LastSessionAt,
    decimal? Adherence7d,
    decimal? Adherence28d,
    NextAssignmentRef? NextAssignment);

internal sealed record ClientDetailResponse(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    ClientStatus Status,
    DateTime JoinedAt,
    DateTime? LastSessionAt,
    decimal? Adherence7d,
    decimal? Adherence28d,
    NextAssignmentRef? NextAssignment,
    string? Goals,
    string? TrainerNotes,
    Guid? InProgressSessionId);
