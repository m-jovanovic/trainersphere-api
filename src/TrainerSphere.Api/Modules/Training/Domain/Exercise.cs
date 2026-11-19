using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// The controlled vocabulary for exercises, in the order of the seed file.
/// </summary>
internal sealed record ExerciseTaxonomy(
    IReadOnlyList<string> Categories,
    IReadOnlyList<string> MovementPatterns,
    IReadOnlyList<string> Muscles,
    IReadOnlyList<string> Equipment);

/// <summary>
/// The compact form of an exercise embedded in workouts, sessions, records and progress.
/// </summary>
internal sealed record ExerciseSummary(Guid Id, string? Slug, string Name, string? Illustration);

internal sealed record ExerciseDefinition(
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment,
    bool Unilateral,
    string? Cue)
{
    public const int NameMaxLength = 100;
    public const int CueMaxLength = 1000;

    public ExerciseDefinition Normalized() =>
        this with
        {
            Name = Name.Trim(),
            PrimaryMuscles = [.. PrimaryMuscles.Distinct(StringComparer.Ordinal)],
            SecondaryMuscles = [.. SecondaryMuscles.Distinct(StringComparer.Ordinal)],
            Equipment = [.. Equipment.Distinct(StringComparer.Ordinal)],
            Cue = Text.NullIfBlank(Cue),
        };
}

/// <summary>
/// A library exercise (no trainer, seeded, read-only) or a trainer's custom exercise.
/// </summary>
internal sealed class Exercise
{
    private Exercise(ExerciseRecord record)
    {
        Id = record.Id;
        TrainerId = record.TrainerId;
        Slug = record.Slug;
        Name = record.Name;
        Category = record.Category;
        MovementPattern = record.MovementPattern;
        PrimaryMuscles = record.PrimaryMuscles;
        SecondaryMuscles = record.SecondaryMuscles;
        Equipment = record.Equipment;
        Unilateral = record.Unilateral;
        Cue = record.Cue;
        Illustration = record.Illustration;
        Archived = record.Archived;
        CreatedAt = record.CreatedAt;
        UpdatedAt = record.UpdatedAt;
    }

    public Guid Id { get; }

    public Guid? TrainerId { get; }

    public string? Slug { get; }

    public string Name { get; private set; }

    public string Category { get; private set; }

    public string MovementPattern { get; private set; }

    public IReadOnlyList<string> PrimaryMuscles { get; private set; }

    public IReadOnlyList<string> SecondaryMuscles { get; private set; }

    public IReadOnlyList<string> Equipment { get; private set; }

    public bool Unilateral { get; private set; }

    public string? Cue { get; private set; }

    public string? Illustration { get; }

    public bool Archived { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsCustom => TrainerId is not null;

    public ExerciseSummary Summary => new(Id, Slug, Name, Illustration);

    public static Exercise CreateCustom(Guid id, Guid trainerId, ExerciseDefinition definition, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        ExerciseDefinition normalized = definition.Normalized();

        return new Exercise(new ExerciseRecord
        {
            Id = id,
            TrainerId = trainerId,
            Name = normalized.Name,
            Category = normalized.Category,
            MovementPattern = normalized.MovementPattern,
            PrimaryMuscles = [.. normalized.PrimaryMuscles],
            SecondaryMuscles = [.. normalized.SecondaryMuscles],
            Equipment = [.. normalized.Equipment],
            Unilateral = normalized.Unilateral,
            Cue = normalized.Cue,
            CreatedAt = now,
            UpdatedAt = now,
        });
    }

    public static Exercise Restore(ExerciseRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new Exercise(record);
    }

    /// <summary>
    /// Library exercises are visible to everyone; custom ones to their trainer and that trainer's clients.
    /// </summary>
    public bool IsVisibleTo(Viewer viewer) => TrainerId is null || TrainerId == viewer.TrainerScope;

    public Result<Exercise> Update(ExerciseDefinition definition, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        if (!IsCustom)
        {
            return ExerciseErrors.NotEditable;
        }

        ExerciseDefinition normalized = definition.Normalized();
        Name = normalized.Name;
        Category = normalized.Category;
        MovementPattern = normalized.MovementPattern;
        PrimaryMuscles = normalized.PrimaryMuscles;
        SecondaryMuscles = normalized.SecondaryMuscles;
        Equipment = normalized.Equipment;
        Unilateral = normalized.Unilateral;
        Cue = normalized.Cue;
        UpdatedAt = now;
        return this;
    }

    /// <summary>
    /// Hides a custom exercise from the library; workouts, sessions and records that use it keep working.
    /// </summary>
    public Result<Exercise> Archive(DateTime now)
    {
        if (!IsCustom)
        {
            return ExerciseErrors.NotEditable;
        }

        if (!Archived)
        {
            Archived = true;
            UpdatedAt = now;
        }

        return this;
    }
}

internal sealed record ExerciseRecord
{
    public Guid Id { get; init; }

    public Guid? TrainerId { get; init; }

    public string? Slug { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Category { get; init; } = string.Empty;

    public string MovementPattern { get; init; } = string.Empty;

    public string[] PrimaryMuscles { get; init; } = [];

    public string[] SecondaryMuscles { get; init; } = [];

    public string[] Equipment { get; init; } = [];

    public bool Unilateral { get; init; }

    public string? Cue { get; init; }

    public string? Illustration { get; init; }

    public bool Archived { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
