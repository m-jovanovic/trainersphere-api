using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class ProgressTests
{
    private static readonly TimeZoneInfo Utc = TimeZoneInfo.Utc;

    [Fact]
    public void Points_groups_sets_per_session_oldest_first()
    {
        var older = Guid.NewGuid();
        var newer = Guid.NewGuid();
        ProgressSet[] sets =
        [
            new(newer, new DateTime(2026, 11, 18, 17, 0, 0, DateTimeKind.Utc), 85, 6),
            new(newer, new DateTime(2026, 11, 18, 17, 5, 0, DateTimeKind.Utc), 80, 8),
            new(older, new DateTime(2026, 11, 14, 17, 0, 0, DateTimeKind.Utc), 80, 8),
        ];

        IReadOnlyList<ProgressPoint> points = ExerciseProgress.Points(sets, Utc);

        points.Count.ShouldBe(2);
        points[0].SessionId.ShouldBe(older);
        points[0].Date.ShouldBe(new DateOnly(2026, 11, 14));
        ProgressPoint point = points[1];
        point.Date.ShouldBe(new DateOnly(2026, 11, 18));
        point.TopSetWeightKg.ShouldBe(85m);
        point.TopSetReps.ShouldBe(6);
        point.EstimatedOneRepMaxKg.ShouldBe(102m);
        point.VolumeKg.ShouldBe((85 * 6) + (80 * 8));
        point.TotalReps.ShouldBe(14);
        point.CompletedSets.ShouldBe(2);
    }

    [Fact]
    public void Points_dates_are_taken_in_the_client_time_zone()
    {
        var auckland = TimeZoneInfo.FindSystemTimeZoneById("Pacific/Auckland");
        var sessionId = Guid.NewGuid();
        ProgressSet[] sets = [new(sessionId, new DateTime(2026, 11, 18, 13, 0, 0, DateTimeKind.Utc), 80, 8)];

        ExerciseProgress.Points(sets, Utc)[0].Date.ShouldBe(new DateOnly(2026, 11, 18));
        ExerciseProgress.Points(sets, auckland)[0].Date.ShouldBe(new DateOnly(2026, 11, 19));
    }

    [Fact]
    public void Weeks_buckets_sessions_into_iso_weeks_up_to_the_current_one()
    {
        SessionVolume[] sessions =
        [
            new(Guid.NewGuid(), new DateTime(2026, 11, 17, 18, 0, 0, DateTimeKind.Utc), 1000m, 10),
            new(Guid.NewGuid(), new DateTime(2026, 11, 10, 18, 0, 0, DateTimeKind.Utc), 2000m, 20),
        ];

        IReadOnlyList<WeekSummary> weeks = ExerciseProgress.Weeks(sessions, Utc, new DateOnly(2026, 11, 18), 3);

        weeks.Count.ShouldBe(3);
        weeks[2].WeekStart.ShouldBe(new DateOnly(2026, 11, 16));
        weeks[2].Sessions.ShouldBe(1);
        weeks[2].VolumeKg.ShouldBe(1000m);
        weeks[1].WeekStart.ShouldBe(new DateOnly(2026, 11, 9));
        weeks[1].Sessions.ShouldBe(1);
        weeks[1].CompletedSets.ShouldBe(20);
        weeks[0].Sessions.ShouldBe(0);
    }
}
