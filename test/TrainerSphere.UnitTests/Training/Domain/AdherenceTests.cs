using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class AdherenceTests
{
    private static readonly DateOnly Today = new(2026, 11, 18); // a Wednesday

    [Fact]
    public void Due_excludes_assignments_dated_today_that_are_still_scheduled()
    {
        ScheduledItem[] items =
        [
            new(Today, AssignmentStatus.Scheduled),
            new(Today, AssignmentStatus.Completed),
            new(Today.AddDays(-1), AssignmentStatus.Scheduled),
        ];

        AdherenceStats stats = Adherence.LastDays(items, Today, 7);

        stats.Due.ShouldBe(2);
        stats.Completed.ShouldBe(1);
        stats.Rate.ShouldBe(0.5m);
    }

    [Fact]
    public void Rate_is_null_when_nothing_was_due()
    {
        AdherenceStats stats = Adherence.LastDays([new ScheduledItem(Today, AssignmentStatus.Scheduled)], Today, 7);

        stats.Due.ShouldBe(0);
        stats.Rate.ShouldBeNull();
    }

    [Fact]
    public void Rate_is_rounded_to_two_decimals()
    {
        ScheduledItem[] items =
        [
            new(Today.AddDays(-1), AssignmentStatus.Completed),
            new(Today.AddDays(-2), AssignmentStatus.Completed),
            new(Today.AddDays(-3), AssignmentStatus.Skipped),
        ];

        Adherence.LastDays(items, Today, 7).Rate.ShouldBe(0.67m);
    }

    [Fact]
    public void Window_is_inclusive_of_both_ends()
    {
        ScheduledItem[] items =
        [
            new(Today.AddDays(-7), AssignmentStatus.Completed),
            new(Today.AddDays(-6), AssignmentStatus.Completed),
        ];

        Adherence.LastDays(items, Today, 7).Due.ShouldBe(1);
        Adherence.LastDays(items, Today, 8).Due.ShouldBe(2);
    }

    [Fact]
    public void ThisWeek_starts_on_monday()
    {
        ScheduledItem[] items =
        [
            new(new DateOnly(2026, 11, 16), AssignmentStatus.Completed), // Monday
            new(new DateOnly(2026, 11, 15), AssignmentStatus.Completed), // Sunday before
        ];

        AdherenceStats stats = Adherence.ThisWeek(items, Today);

        stats.Due.ShouldBe(1);
        IsoWeek.Start(Today).ShouldBe(new DateOnly(2026, 11, 16));
        IsoWeek.Start(new DateOnly(2026, 11, 15)).ShouldBe(new DateOnly(2026, 11, 9));
    }

    [Fact]
    public void Streak_counts_consecutive_weeks_back_from_the_current_one()
    {
        DateOnly[] dates =
        [
            new(2026, 11, 17), // current week
            new(2026, 11, 10), // week before
            new(2026, 10, 20), // three weeks back: the gap at 11-02 breaks the streak
        ];

        Streaks.Weeks(dates, Today).ShouldBe(2);
    }

    [Fact]
    public void Streak_skips_the_current_week_when_it_has_no_session_yet()
    {
        DateOnly[] dates = [new(2026, 11, 11), new(2026, 11, 4)];

        Streaks.Weeks(dates, Today).ShouldBe(2);
    }

    [Fact]
    public void Streak_is_zero_without_recent_sessions()
    {
        Streaks.Weeks([new DateOnly(2026, 10, 1)], Today).ShouldBe(0);
        Streaks.Weeks([], Today).ShouldBe(0);
    }

    [Fact]
    public void Sessions_after_today_do_not_count() =>
        Streaks.Weeks([Today.AddDays(7)], Today).ShouldBe(0);
}
