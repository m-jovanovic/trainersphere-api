namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// The stored status and date of one assignment, as needed for adherence.
/// </summary>
internal sealed record ScheduledItem(DateOnly Date, AssignmentStatus Status);

internal sealed record AdherenceStats(int Due, int Completed)
{
    /// <summary>
    /// Gets completed / due rounded to 2 decimals, or null when nothing was due.
    /// </summary>
    public decimal? Rate => Due == 0 ? null : decimal.Round((decimal)Completed / Due, 2, MidpointRounding.AwayFromZero);
}

/// <summary>
/// Adherence over a window ending today (inclusive): every assignment dated in the window is due, except those
/// dated today that are still scheduled (the client may still do them). Completed ones count as done.
/// </summary>
internal static class Adherence
{
    public static AdherenceStats Over(IEnumerable<ScheduledItem> items, DateOnly from, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(items);

        ScheduledItem[] due =
        [
            .. items.Where(i => i.Date >= from && i.Date <= today && !(i.Date == today && i.Status == AssignmentStatus.Scheduled)),
        ];

        return new AdherenceStats(due.Length, due.Count(i => i.Status == AssignmentStatus.Completed));
    }

    public static AdherenceStats LastDays(IEnumerable<ScheduledItem> items, DateOnly today, int days) =>
        Over(items, today.AddDays(1 - days), today);

    /// <summary>
    /// The ISO week (Monday start) up to and including today.
    /// </summary>
    public static AdherenceStats ThisWeek(IEnumerable<ScheduledItem> items, DateOnly today) =>
        Over(items, IsoWeek.Start(today), today);
}

internal static class IsoWeek
{
    public static DateOnly Start(DateOnly date) => date.AddDays(-(((int)date.DayOfWeek + 6) % 7));
}

internal static class Streaks
{
    /// <summary>
    /// Consecutive ISO weeks with at least one completed session, counting back from the current week. The current
    /// week does not break the streak while it has no session yet.
    /// </summary>
    public static int Weeks(IEnumerable<DateOnly> completedSessionDates, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(completedSessionDates);

        HashSet<DateOnly> weeks = [.. completedSessionDates.Where(d => d <= today).Select(IsoWeek.Start)];
        DateOnly week = IsoWeek.Start(today);

        if (!weeks.Contains(week))
        {
            week = week.AddDays(-7);
        }

        int streak = 0;

        while (weeks.Contains(week))
        {
            streak++;
            week = week.AddDays(-7);
        }

        return streak;
    }
}

internal static class LocalDates
{
    public static DateOnly Of(DateTime utc, TimeZoneInfo zone) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), zone));
}
