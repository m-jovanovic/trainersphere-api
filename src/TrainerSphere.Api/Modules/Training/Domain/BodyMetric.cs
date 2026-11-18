namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// The measurements of one client on one date. A PUT replaces the whole entry for that date.
/// </summary>
internal sealed record BodyMetric(
    Guid ClientId,
    DateOnly Date,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes,
    RecordedBy RecordedBy,
    DateTime UpdatedAt)
{
    public const decimal MinWeightKg = 20m;
    public const decimal MaxWeightKg = 400m;
    public const decimal MinBodyFat = 2m;
    public const decimal MaxBodyFat = 75m;
    public const decimal MinCircumference = 10m;
    public const decimal MaxCircumference = 300m;
    public const int MinHeartRate = 25;
    public const int MaxHeartRate = 250;
    public const int NotesMaxLength = 1000;

    public bool HasAnyMeasurement =>
        WeightKg is not null || BodyFatPercent is not null || WaistCm is not null || ChestCm is not null ||
        HipsCm is not null || ArmCm is not null || ThighCm is not null || RestingHeartRate is not null;

    /// <summary>
    /// Measurements cannot be recorded for a date after the client's today.
    /// </summary>
    public static bool IsDateAllowed(DateOnly date, DateOnly clientToday) => date <= clientToday;
}
