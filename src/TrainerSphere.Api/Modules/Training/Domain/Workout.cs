namespace TrainerSphere.Api.Modules.Training.Domain;

internal sealed record Goal(GoalType Type, int Min, int Max)
{
    public const int MaxReps = 100;
    public const int MaxSeconds = 7200;
    public const int MaxMeters = 100_000;

    public static int UpperBound(GoalType type) => type switch
    {
        GoalType.Reps => MaxReps,
        GoalType.Time => MaxSeconds,
        GoalType.Distance => MaxMeters,
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, null),
    };
}

internal sealed record Load(LoadType Type, decimal? Value)
{
    public const decimal MaxWeightKg = 1000m;
    public const decimal MinPercent = 1m;
    public const decimal MaxPercent = 120m;
    public const decimal MinRpe = 1m;
    public const decimal MaxRpe = 10m;

    /// <summary>
    /// Checks the value against the load type: none has no value, bodyweight optionally carries added kg, weight is
    /// kg, percent-1rm a percentage and rpe a 0.5-step effort score.
    /// </summary>
    public static string? Check(LoadType type, decimal? value) => type switch
    {
        LoadType.None => value is null ? null : "A load of type 'none' has no value.",
        LoadType.Bodyweight => value is null || IsWeight(value.Value) ? null : "Added weight must be between 0 and 1000 kg with at most 2 decimals.",
        LoadType.Weight => value is { } kg && IsWeight(kg) ? null : "Weight must be between 0 and 1000 kg with at most 2 decimals.",
        LoadType.Percent1Rm => value is { } percent && percent >= MinPercent && percent <= MaxPercent && Decimals.HasAtMost(percent, 2)
            ? null
            : "Percentage of 1RM must be between 1 and 120.",
        LoadType.Rpe => value is { } rpe && Decimals.IsRpe(rpe) ? null : "RPE must be between 1 and 10 in steps of 0.5.",
        _ => "Unknown load type.",
    };

    private static bool IsWeight(decimal kg) => kg >= 0 && kg <= MaxWeightKg && Decimals.HasAtMost(kg, 2);
}

internal static class Decimals
{
    public static bool HasAtMost(decimal value, int decimals) => decimal.Round(value, decimals) == value;

    public static bool IsRpe(decimal value) => value >= Load.MinRpe && value <= Load.MaxRpe && decimal.Round(value * 2) == value * 2;
}

internal sealed record WorkoutExerciseDefinition(
    Guid ExerciseId,
    int Sets,
    Goal Goal,
    Load Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes);

internal sealed record WorkoutDefinition(string Name, string? Description, IReadOnlyList<WorkoutExerciseDefinition> Exercises)
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
    public const int NotesMaxLength = 1000;
    public const int MaxExercises = 30;
    public const int MaxSets = 20;
    public const int MaxRestSeconds = 1800;
}

internal sealed record WorkoutExercise(
    Guid Id,
    int Position,
    Guid ExerciseId,
    int Sets,
    Goal Goal,
    Load Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes);

/// <summary>
/// A prescribed exercise as it is shown and snapshotted: the template line plus the exercise summary.
/// </summary>
internal sealed record WorkoutExerciseView(
    Guid Id,
    int Position,
    Guid ExerciseId,
    ExerciseSummary Exercise,
    int Sets,
    Goal Goal,
    Load Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes);

/// <summary>
/// The copy of a workout an assignment keeps, so later template edits do not change scheduled or past work.
/// </summary>
internal sealed record WorkoutSnapshot(string Name, string? Description, IReadOnlyList<WorkoutExerciseView> Exercises);

/// <summary>
/// A trainer's workout template: an ordered list of prescribed exercises.
/// </summary>
internal sealed class Workout
{
    private const string CopySuffix = " (copy)";

    private Workout(Guid id, Guid trainerId, string name, string? description, bool archived, DateTime createdAt, DateTime updatedAt, IReadOnlyList<WorkoutExercise> exercises)
    {
        Id = id;
        TrainerId = trainerId;
        Name = name;
        Description = description;
        Archived = archived;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Exercises = exercises;
    }

    public Guid Id { get; }

    public Guid TrainerId { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public bool Archived { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<WorkoutExercise> Exercises { get; private set; }

    public static Workout Create(Guid id, Guid trainerId, WorkoutDefinition definition, Func<Guid> newId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return new Workout(id, trainerId, definition.Name.Trim(), Text.NullIfBlank(definition.Description), archived: false, now, now, Lines(definition, newId));
    }

    public static Workout Restore(WorkoutRecord record, IEnumerable<WorkoutExercise> exercises)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new Workout(record.Id, record.TrainerId, record.Name, record.Description, record.Archived, record.CreatedAt, record.UpdatedAt, [.. exercises.OrderBy(e => e.Position)]);
    }

    /// <summary>
    /// The name of a duplicate, shortened so the suffix still fits the name limit.
    /// </summary>
    public static string CopyName(string name)
    {
        ArgumentNullException.ThrowIfNull(name);

        int room = WorkoutDefinition.NameMaxLength - CopySuffix.Length;
        return (name.Length > room ? name[..room].TrimEnd() : name) + CopySuffix;
    }

    public static WorkoutExerciseView View(WorkoutExercise line, IReadOnlyDictionary<Guid, ExerciseSummary> exercises)
    {
        ArgumentNullException.ThrowIfNull(line);
        ArgumentNullException.ThrowIfNull(exercises);

        ExerciseSummary exercise = exercises.TryGetValue(line.ExerciseId, out ExerciseSummary? found)
            ? found
            : new ExerciseSummary(line.ExerciseId, null, string.Empty, null);

        return new WorkoutExerciseView(line.Id, line.Position, line.ExerciseId, exercise, line.Sets, line.Goal, line.Load, line.RestSeconds, line.Tempo, line.SupersetGroup, line.Notes);
    }

    public void Replace(WorkoutDefinition definition, Func<Guid> newId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Name = definition.Name.Trim();
        Description = Text.NullIfBlank(definition.Description);
        Exercises = Lines(definition, newId);
        UpdatedAt = now;
    }

    public Workout Duplicate(Guid id, Func<Guid> newId, DateTime now) =>
        new(id, TrainerId, CopyName(Name), Description, archived: false, now, now, [.. Exercises.Select(e => e with { Id = newId() })]);

    public void Archive(DateTime now)
    {
        if (!Archived)
        {
            Archived = true;
            UpdatedAt = now;
        }
    }

    public WorkoutSnapshot Snapshot(IReadOnlyDictionary<Guid, ExerciseSummary> exercises) =>
        new(Name, Description, [.. Exercises.Select(e => View(e, exercises))]);

    private static WorkoutExercise[] Lines(WorkoutDefinition definition, Func<Guid> newId) =>
        [
            .. definition.Exercises.Select((e, index) => new WorkoutExercise(
                newId(),
                index + 1,
                e.ExerciseId,
                e.Sets,
                e.Goal,
                e.Load,
                e.RestSeconds,
                Text.NullIfBlank(e.Tempo),
                Text.NullIfBlank(e.SupersetGroup),
                Text.NullIfBlank(e.Notes))),
        ];
}

internal sealed record WorkoutRecord
{
    public Guid Id { get; init; }

    public Guid TrainerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public bool Archived { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Superset rule: consecutive exercises with the same letter form one superset, so a letter must label one
/// unbroken run of at least two exercises.
/// </summary>
internal static class SupersetGroups
{
    public static bool IsLetter(string? group) => group is null || (group.Length == 1 && char.IsAsciiLetterUpper(group[0]));

    /// <summary>
    /// Returns the index and message of every exercise whose superset letter breaks the rule.
    /// </summary>
    public static IReadOnlyList<(int Index, string Message)> Check(IReadOnlyList<string?> groups)
    {
        ArgumentNullException.ThrowIfNull(groups);

        List<(int Index, string Message)> problems = [];
        HashSet<string> closed = new(StringComparer.Ordinal);
        int start = 0;

        while (start < groups.Count)
        {
            string? letter = groups[start];
            int end = start;

            while (end + 1 < groups.Count && string.Equals(groups[end + 1], letter, StringComparison.Ordinal))
            {
                end++;
            }

            if (letter is not null)
            {
                string? message = null;

                if (!closed.Add(letter))
                {
                    message = $"Superset group '{letter}' must be used by consecutive exercises only.";
                }
                else if (end == start)
                {
                    message = $"Superset group '{letter}' needs at least two consecutive exercises.";
                }

                if (message is not null)
                {
                    problems.AddRange(Enumerable.Range(start, end - start + 1).Select(index => (index, message)));
                }
            }

            start = end + 1;
        }

        return problems;
    }
}
