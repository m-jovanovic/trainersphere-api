namespace TrainerSphere.SharedKernel.Domain;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Returns the current time as a UTC <see cref="DateTime"/> (Npgsql only accepts UTC kinds for timestamptz),
    /// truncated to the microsecond precision Postgres stores, so a value returned right after a write equals the
    /// value read back later.
    /// </summary>
    public static DateTime UtcNow(this TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        DateTime now = timeProvider.GetUtcNow().UtcDateTime;
        return new DateTime(now.Ticks - (now.Ticks % TimeSpan.TicksPerMicrosecond), DateTimeKind.Utc);
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
