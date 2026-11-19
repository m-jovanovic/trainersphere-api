namespace TrainerSphere.IntegrationTests.Builders;

/// <summary>
/// Calendar dates in UTC (the time zone Training tests give their users): <c>yyyy-MM-dd</c>.
/// </summary>
public static class TrainingDates
{
    public static string Today => Offset(0);

    public static string Yesterday => Offset(-1);

    public static string Tomorrow => Offset(1);

    public static string Offset(int days) =>
        DateOnly.FromDateTime(DateTime.UtcNow).AddDays(days).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
