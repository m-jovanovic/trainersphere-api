namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// A completed set of one exercise in a completed session, with the session's start time.
/// </summary>
internal sealed record ProgressSet(Guid SessionId, DateTime StartedAt, decimal? WeightKg, int? Reps);

/// <summary>
/// One session's performance of one exercise. The top set is the heaviest (most reps on a tie); bodyweight sets
/// count as 0 kg.
/// </summary>
internal sealed record ProgressPoint(
    DateOnly Date,
    Guid SessionId,
    decimal? EstimatedOneRepMaxKg,
    decimal? TopSetWeightKg,
    int? TopSetReps,
    decimal VolumeKg,
    int TotalReps,
    int CompletedSets);

/// <summary>
/// A completed session with its volume (weight x reps of completed sets) and completed set count.
/// </summary>
internal sealed record SessionVolume(Guid Id, DateTime StartedAt, decimal VolumeKg, int CompletedSets);

internal sealed record WeekSummary(DateOnly WeekStart, int Sessions, decimal VolumeKg, int CompletedSets);

internal static class ExerciseProgress
{
    /// <summary>
    /// One point per session, oldest first, dated by the session start in the client's time zone.
    /// </summary>
    public static IReadOnlyList<ProgressPoint> Points(IEnumerable<ProgressSet> sets, TimeZoneInfo zone)
    {
        ArgumentNullException.ThrowIfNull(sets);

        return
        [
            .. sets.GroupBy(s => s.SessionId)
                .Select(session => (StartedAt: session.Min(s => s.StartedAt), Point: PointOf(session.Key, [.. session], zone)))
                .OrderBy(p => p.StartedAt)
                .Select(p => p.Point),
        ];
    }

    /// <summary>
    /// The last <paramref name="weeks"/> ISO weeks up to the current one, oldest first, with the completed sessions
    /// dated (in the client's time zone) in each.
    /// </summary>
    public static IReadOnlyList<WeekSummary> Weeks(IEnumerable<SessionVolume> sessions, TimeZoneInfo zone, DateOnly today, int weeks)
    {
        ArgumentNullException.ThrowIfNull(sessions);

        ILookup<DateOnly, SessionVolume> byWeek = sessions.ToLookup(s => IsoWeek.Start(LocalDates.Of(s.StartedAt, zone)));
        DateOnly current = IsoWeek.Start(today);

        return
        [
            .. Enumerable.Range(0, weeks)
                .Select(index => current.AddDays(-7 * (weeks - 1 - index)))
                .Select(start => new WeekSummary(start, byWeek[start].Count(), byWeek[start].Sum(s => s.VolumeKg), byWeek[start].Sum(s => s.CompletedSets))),
        ];
    }

    private static ProgressPoint PointOf(Guid sessionId, ProgressSet[] sets, TimeZoneInfo zone)
    {
        ProgressSet top = sets.OrderByDescending(s => s.WeightKg ?? 0).ThenByDescending(s => s.Reps ?? 0).First();

        return new ProgressPoint(
            LocalDates.Of(sets.Min(s => s.StartedAt), zone),
            sessionId,
            sets.Max(s => PersonalRecords.EstimatedOneRepMax(s.WeightKg, s.Reps)),
            top.WeightKg,
            top.Reps,
            sets.Sum(s => (s.WeightKg ?? 0) * (s.Reps ?? 0)),
            sets.Sum(s => s.Reps ?? 0),
            sets.Length);
    }
}
