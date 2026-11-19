using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Domain;

internal sealed record ProgramSlotDefinition(int Week, int Day, Guid WorkoutId);

internal sealed record ProgramDefinition(string Name, string? Description, int Weeks, IReadOnlyList<ProgramSlotDefinition> Slots)
{
    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 2000;
}

/// <summary>
/// One workout in the program grid. <see cref="Ordinal"/> keeps the input order of slots on the same day.
/// </summary>
internal sealed record ProgramSlot(Guid Id, int Week, int Day, int Ordinal, Guid WorkoutId);

/// <summary>
/// A trainer's program: a grid of N weeks x 7 days, each day holding up to three workout templates.
/// Day 1 is the weekday of the start date the program is assigned with.
/// </summary>
internal sealed class TrainingProgram
{
    public const int MaxWeeks = 52;
    public const int DaysPerWeek = 7;
    public const int MaxSlotsPerDay = 3;

    private TrainingProgram(ProgramRecord record, IReadOnlyList<ProgramSlot> slots)
    {
        Id = record.Id;
        TrainerId = record.TrainerId;
        Name = record.Name;
        Description = record.Description;
        Weeks = record.Weeks;
        Archived = record.Archived;
        CreatedAt = record.CreatedAt;
        UpdatedAt = record.UpdatedAt;
        Slots = slots;
    }

    public Guid Id { get; }

    public Guid TrainerId { get; }

    public string Name { get; private set; }

    public string? Description { get; private set; }

    public int Weeks { get; private set; }

    public bool Archived { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    public IReadOnlyList<ProgramSlot> Slots { get; private set; }

    public static Result<TrainingProgram> Create(Guid id, Guid trainerId, ProgramDefinition definition, Func<Guid> newId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return CheckGrid(definition.Weeks, definition.Slots)
            .Map(_ => new TrainingProgram(
                new ProgramRecord
                {
                    Id = id,
                    TrainerId = trainerId,
                    Name = definition.Name.Trim(),
                    Description = Text.NullIfBlank(definition.Description),
                    Weeks = definition.Weeks,
                    CreatedAt = now,
                    UpdatedAt = now,
                },
                SlotsFrom(definition, newId)));
    }

    public static TrainingProgram Restore(ProgramRecord record, IEnumerable<ProgramSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new TrainingProgram(record, Sorted(slots));
    }

    /// <summary>
    /// Every slot must fall inside the grid, and a day holds at most <see cref="MaxSlotsPerDay"/> workouts.
    /// </summary>
    public static Result<Unit> CheckGrid(int weeks, IReadOnlyList<ProgramSlotDefinition> slots)
    {
        ArgumentNullException.ThrowIfNull(slots);

        ProgramSlotDefinition? outside = slots.FirstOrDefault(s => s.Week < 1 || s.Week > weeks || s.Day < 1 || s.Day > DaysPerWeek);

        if (outside is not null)
        {
            return ProgramErrors.SlotOutOfRange(outside.Week, outside.Day, weeks);
        }

        IGrouping<(int Week, int Day), ProgramSlotDefinition>? crowded = slots
            .GroupBy(s => (s.Week, s.Day))
            .FirstOrDefault(day => day.Count() > MaxSlotsPerDay);

        return crowded is null ? Result.Success() : ProgramErrors.TooManySlots(crowded.Key.Week, crowded.Key.Day, MaxSlotsPerDay);
    }

    /// <summary>
    /// The calendar date of a slot when the program starts on <paramref name="startDate"/>.
    /// </summary>
    public static DateOnly DateOf(DateOnly startDate, int week, int day) => startDate.AddDays(((week - 1) * DaysPerWeek) + (day - 1));

    public static DateOnly EndDate(DateOnly startDate, int weeks) => startDate.AddDays((weeks * DaysPerWeek) - 1);

    public Result<TrainingProgram> Replace(ProgramDefinition definition, Func<Guid> newId, DateTime now)
    {
        ArgumentNullException.ThrowIfNull(definition);

        return CheckGrid(definition.Weeks, definition.Slots)
            .Map(_ =>
            {
                Name = definition.Name.Trim();
                Description = Text.NullIfBlank(definition.Description);
                Weeks = definition.Weeks;
                Slots = SlotsFrom(definition, newId);
                UpdatedAt = now;
                return this;
            });
    }

    public Result<TrainingProgram> EnsureActive() => Archived ? ProgramErrors.Archived(Id) : this;

    public void Archive(DateTime now)
    {
        if (!Archived)
        {
            Archived = true;
            UpdatedAt = now;
        }
    }

    public IReadOnlyList<(ProgramSlot Slot, DateOnly Date)> Schedule(DateOnly startDate) =>
        [.. Slots.Select(slot => (slot, DateOf(startDate, slot.Week, slot.Day)))];

    private static ProgramSlot[] SlotsFrom(ProgramDefinition definition, Func<Guid> newId) =>
        Sorted(definition.Slots.Select((s, index) => new ProgramSlot(newId(), s.Week, s.Day, index + 1, s.WorkoutId)));

    private static ProgramSlot[] Sorted(IEnumerable<ProgramSlot> slots) =>
        [.. slots.OrderBy(s => s.Week).ThenBy(s => s.Day).ThenBy(s => s.Ordinal)];
}

internal sealed record ProgramRecord
{
    public Guid Id { get; init; }

    public Guid TrainerId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    public int Weeks { get; init; }

    public bool Archived { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// A program scheduled for one client from a start date; it created one assignment per slot.
/// </summary>
internal sealed record ProgramAssignment(
    Guid Id,
    Guid ProgramId,
    string ProgramName,
    Guid TrainerId,
    Guid ClientId,
    DateOnly StartDate,
    DateOnly EndDate,
    DateTime CreatedAt);
