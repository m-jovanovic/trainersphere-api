using TrainerSphere.Api.Modules.Training.Domain;

namespace TrainerSphere.UnitTests.Training.Domain;

public sealed class BodyMetricTests
{
    [Fact]
    public void An_entry_without_any_measurement_is_empty()
    {
        BodyMetric metric = Metric();

        metric.HasAnyMeasurement.ShouldBeFalse();
        Metric(weightKg: 80).HasAnyMeasurement.ShouldBeTrue();
        Metric(restingHeartRate: 55).HasAnyMeasurement.ShouldBeTrue();
    }

    [Fact]
    public void Future_dates_are_rejected_in_the_client_time_zone()
    {
        DateOnly today = new(2026, 11, 18);

        BodyMetric.IsDateAllowed(today, today).ShouldBeTrue();
        BodyMetric.IsDateAllowed(today.AddDays(1), today).ShouldBeFalse();
    }

    private static BodyMetric Metric(decimal? weightKg = null, int? restingHeartRate = null) =>
        new(
            TrainingTestData.ClientId,
            TrainingTestData.Today,
            weightKg,
            null,
            null,
            null,
            null,
            null,
            null,
            restingHeartRate,
            null,
            RecordedBy.Client,
            TrainingTestData.Now);
}
