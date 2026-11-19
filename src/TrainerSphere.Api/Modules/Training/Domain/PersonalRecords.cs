namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// A completed set used for record detection. Bodyweight sets have no weight.
/// </summary>
internal sealed record LoggedSet(Guid SessionId, Guid ExerciseId, decimal? WeightKg, int? Reps);

internal sealed record PersonalRecord(
    Guid Id,
    Guid ClientId,
    Guid TrainerId,
    Guid ExerciseId,
    PersonalRecordType Type,
    decimal Value,
    decimal PreviousValue,
    decimal? WeightKg,
    int? Reps,
    Guid SessionId,
    DateTime AchievedAt);

internal sealed record PersonalRecordCandidate(
    Guid ExerciseId,
    PersonalRecordType Type,
    decimal Value,
    decimal PreviousValue,
    decimal? WeightKg,
    int? Reps);

/// <summary>
/// Personal record rules. Only completed sets count. The first session that logs an exercise sets the baseline
/// and is never reported, so every record has a numeric previous value.
/// </summary>
internal static class PersonalRecords
{
    public const int MaxRepsForEstimate = 12;

    /// <summary>
    /// Epley estimate <c>w * (1 + reps / 30)</c> for 1-12 reps (one rep is the weight itself), rounded to 2 decimals;
    /// null when the set does not qualify.
    /// </summary>
    public static decimal? EstimatedOneRepMax(decimal? weightKg, int? reps) =>
        (weightKg, reps) switch
        {
            ({ } w, { } r) when w > 0 && r == 1 => w,
            ({ } w, { } r) when w > 0 && r > 1 && r <= MaxRepsForEstimate => decimal.Round(w * (1 + (r / 30m)), 2),
            _ => null,
        };

    /// <summary>
    /// Detects the records the sets of one session set against all earlier history of the client. At most one
    /// record per exercise and type is reported: the best set of the session.
    /// </summary>
    public static IReadOnlyList<PersonalRecordCandidate> Detect(IReadOnlyList<LoggedSet> session, IReadOnlyList<LoggedSet> history)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(history);

        ILookup<Guid, LoggedSet> previousByExercise = history.ToLookup(s => s.ExerciseId);
        List<PersonalRecordCandidate> records = [];

        foreach (IGrouping<Guid, LoggedSet> exercise in session.GroupBy(s => s.ExerciseId))
        {
            LoggedSet[] current = [.. exercise];
            LoggedSet[] previous = [.. previousByExercise[exercise.Key]];

            if (previous.Length == 0)
            {
                continue;
            }

            AddIfAny(records, EstimatedOneRepMaxRecord(exercise.Key, current, previous));
            AddIfAny(records, MaxWeightRecord(exercise.Key, current, previous));
            AddIfAny(records, MaxRepsRecord(exercise.Key, current, previous));
            AddIfAny(records, MaxVolumeRecord(exercise.Key, current, previous));
        }

        return records;
    }

    /// <summary>
    /// Gets the exercise volume (weight x reps) of a group of completed sets.
    /// </summary>
    public static decimal Volume(IEnumerable<LoggedSet> sets) => sets.Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0));

    private static void AddIfAny(List<PersonalRecordCandidate> records, PersonalRecordCandidate? record)
    {
        if (record is not null)
        {
            records.Add(record);
        }
    }

    private static PersonalRecordCandidate? EstimatedOneRepMaxRecord(Guid exerciseId, LoggedSet[] current, LoggedSet[] previous)
    {
        decimal? best = previous.Max(s => EstimatedOneRepMax(s.WeightKg, s.Reps));
        LoggedSet? top = current
            .Where(s => EstimatedOneRepMax(s.WeightKg, s.Reps) is not null)
            .OrderByDescending(s => EstimatedOneRepMax(s.WeightKg, s.Reps))
            .ThenByDescending(s => s.WeightKg)
            .FirstOrDefault();

        return top is not null && best is { } before && EstimatedOneRepMax(top.WeightKg, top.Reps) is { } value && value > before
            ? new PersonalRecordCandidate(exerciseId, PersonalRecordType.Estimated1Rm, value, before, top.WeightKg, top.Reps)
            : null;
    }

    private static PersonalRecordCandidate? MaxWeightRecord(Guid exerciseId, LoggedSet[] current, LoggedSet[] previous)
    {
        static bool Qualifies(LoggedSet s) => s.WeightKg > 0 && s.Reps >= 1;

        decimal? best = previous.Where(Qualifies).Max(s => s.WeightKg);
        LoggedSet? top = current.Where(Qualifies).OrderByDescending(s => s.WeightKg).ThenByDescending(s => s.Reps).FirstOrDefault();

        return top?.WeightKg is { } value && best is { } before && value > before
            ? new PersonalRecordCandidate(exerciseId, PersonalRecordType.MaxWeight, value, before, value, top.Reps)
            : null;
    }

    /// <summary>
    /// Most reps in one set compared with earlier sets at the same or a higher weight (bodyweight counts as 0 kg),
    /// so a lighter set never beats a heavier one.
    /// </summary>
    private static PersonalRecordCandidate? MaxRepsRecord(Guid exerciseId, LoggedSet[] current, LoggedSet[] previous)
    {
        LoggedSet[] earlier = [.. previous.Where(s => s.Reps >= 1)];

        return current
            .Where(s => s.Reps >= 1)
            .Select(set =>
            {
                decimal weight = set.WeightKg ?? 0;
                int? before = earlier.Where(e => (e.WeightKg ?? 0) >= weight).Max(e => e.Reps);
                return before is { } prior && set.Reps > prior
                    ? new PersonalRecordCandidate(exerciseId, PersonalRecordType.MaxReps, set.Reps.Value, prior, set.WeightKg, set.Reps)
                    : null;
            })
            .OfType<PersonalRecordCandidate>()
            .OrderByDescending(r => r.Value)
            .ThenByDescending(r => r.WeightKg ?? 0)
            .FirstOrDefault();
    }

    private static PersonalRecordCandidate? MaxVolumeRecord(Guid exerciseId, LoggedSet[] current, LoggedSet[] previous)
    {
        decimal value = Volume(current);
        decimal before = previous.GroupBy(s => s.SessionId).Select(Volume).DefaultIfEmpty(0).Max();

        return before > 0 && value > before
            ? new PersonalRecordCandidate(exerciseId, PersonalRecordType.MaxVolume, value, before, null, null)
            : null;
    }
}
