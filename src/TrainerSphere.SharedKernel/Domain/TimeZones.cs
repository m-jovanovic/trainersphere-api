namespace TrainerSphere.SharedKernel.Domain;

public static class TimeZones
{
    public const string Utc = "UTC";

    public static bool IsValid(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out _);

    public static TimeZoneInfo FindOrUtc(string? timeZoneId) =>
        !string.IsNullOrWhiteSpace(timeZoneId) && TimeZoneInfo.TryFindSystemTimeZoneById(timeZoneId, out TimeZoneInfo? zone)
            ? zone
            : TimeZoneInfo.Utc;
}
