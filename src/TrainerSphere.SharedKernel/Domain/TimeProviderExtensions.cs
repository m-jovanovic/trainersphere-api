namespace TrainerSphere.SharedKernel.Domain;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Returns the current time as a UTC <see cref="DateTime"/>; Npgsql only accepts UTC kinds for timestamptz.
    /// </summary>
    public static DateTime UtcNow(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return timeProvider.GetUtcNow().UtcDateTime;
    }

    /// <summary>
    /// Returns today's calendar date in the specified IANA time zone (UTC when unknown).
    /// </summary>
    public static DateOnly Today(this TimeProvider timeProvider, string? timeZoneId)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTime utc = timeProvider.UtcNow();
        TimeZoneInfo zone = TimeZones.FindOrUtc(timeZoneId);

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(utc, zone));
    }
}
