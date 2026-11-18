namespace TrainerSphere.IntegrationTests.Contracts;

// Test-side copies of the Training HTTP contract (sections 6-14), kept as records with enums and calendar dates as
// strings so the tests assert the exact wire values the contract documents.
public sealed record Exercise(
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
    bool Archived);

public sealed record ExerciseTaxonomy(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> MovementPatterns,
    IReadOnlyList<string> Muscles,
    IReadOnlyList<string> Equipment);

public sealed record ExerciseWrite(
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment,
    bool Unilateral,
    string? Cue);

public sealed record WorkoutWrite(string Name, string? Description, IReadOnlyList<WorkoutExerciseWrite> Exercises);

public sealed record WorkoutExerciseWrite(
    Guid ExerciseId,
    int Sets,
    GoalWrite Goal,
    LoadWrite Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes);

public sealed record GoalWrite(string Type, int? Min, int? Max);

public sealed record LoadWrite(string Type, decimal? Value);

public sealed record WorkoutItem(
    Guid Id,
    string Name,
    string? Description,
    bool Archived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<WorkoutExerciseItem> Exercises);

public sealed record WorkoutExerciseItem(
    Guid Id,
    int Position,
    Guid ExerciseId,
    int Sets,
    GoalItem Goal,
    LoadItem Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes);

public sealed record GoalItem(string Type, int? Min, int? Max);

public sealed record LoadItem(string Type, decimal? Value);

public sealed record WorkoutSummary(
    Guid Id,
    string Name,
    string? Description,
    int ExerciseCount,
    bool Archived,
    DateTimeOffset UpdatedAt);

public sealed record ProgramWrite(string Name, string? Description, int Weeks, IReadOnlyList<ProgramSlotWrite> Slots);

public sealed record ProgramSlotWrite(int Week, int Day, Guid WorkoutId);

public sealed record ProgramItem(
    Guid Id,
    string Name,
    string? Description,
    int Weeks,
    bool Archived,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<ProgramSlotItem> Slots);

public sealed record ProgramSlotItem(Guid Id, int Week, int Day, Guid WorkoutId);

public sealed record ProgramSummary(
    Guid Id,
    string Name,
    string? Description,
    int Weeks,
    int WorkoutCount,
    bool Archived,
    DateTimeOffset UpdatedAt);

public sealed record AssignProgramRequest(IReadOnlyList<Guid> ClientIds, string StartDate);

public sealed record ProgramAssignmentItem(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid ClientId,
    string StartDate,
    string EndDate,
    int AssignmentCount,
    DateTimeOffset CreatedAt);

public sealed record AssignmentWrite(Guid ClientId, Guid WorkoutId, string Date, string? Notes);

public sealed record AssignmentUpdate(string Date, string? Notes);

public sealed record AssignmentItem(
    Guid Id,
    Guid ClientId,
    string Date,
    string Status,
    Guid WorkoutId,
    Guid? ProgramAssignmentId,
    string? ProgramName,
    string? Notes,
    Guid? SessionId,
    DateTimeOffset? CompletedAt,
    DateTimeOffset CreatedAt);

public sealed record SetWrite(
    Guid ExerciseId,
    int Position,
    int SetNumber,
    decimal? WeightKg,
    int? Reps,
    int? DurationSeconds,
    int? DistanceMeters,
    decimal? Rpe,
    bool Completed,
    string? Notes);

public sealed record SessionUpdate(string WorkoutName, string? Notes);

public sealed record SessionSummaryItem(
    Guid Id,
    Guid ClientId,
    Guid? AssignmentId,
    string WorkoutName,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? CompletedAt,
    decimal TotalVolumeKg,
    int CompletedSets,
    int PersonalRecordCount);

public sealed record PersonalRecordItem(
    Guid Id,
    Guid ClientId,
    Guid ExerciseId,
    string Type,
    decimal Value,
    decimal PreviousValue,
    decimal? WeightKg,
    int? Reps,
    Guid SessionId,
    DateTimeOffset AchievedAt);

public sealed record BodyMetricWrite(
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes);

public sealed record BodyMetricItem(
    Guid ClientId,
    string Date,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes,
    string RecordedBy,
    DateTimeOffset UpdatedAt);

public sealed record ClientUpdate(string? Goals, string? TrainerNotes);

public sealed record ClientSummaryItem(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string Status,
    DateTimeOffset JoinedAt,
    DateTimeOffset? LastSessionAt,
    decimal? Adherence7d,
    decimal? Adherence28d,
    NextAssignmentItem? NextAssignment);

public sealed record NextAssignmentItem(Guid Id, string Date, string WorkoutName);
