using System.Text.Json.Serialization;

namespace TrainerSphere.Api.Modules.Training.Domain;

internal enum Units
{
    Kg,
    Lb,
}

internal enum ClientStatus
{
    Active,
    Archived,
}

internal enum GoalType
{
    Reps,
    Time,
    Distance,
}

internal enum LoadType
{
    None,
    Bodyweight,
    Weight,

    // The kebab-case naming policy would produce "percent1-rm".
    [JsonStringEnumMemberName("percent-1rm")]
    Percent1Rm,
    Rpe,
}

/// <summary>
/// Only Scheduled, Completed and Skipped are stored. Missed is a scheduled assignment dated before the client's
/// today and is derived at read time.
/// </summary>
internal enum AssignmentStatus
{
    Scheduled,
    Completed,
    Skipped,
    Missed,
}

internal enum SessionStatus
{
    InProgress,
    Completed,
}

internal enum PersonalRecordType
{
    // The kebab-case naming policy would produce "estimated1-rm".
    [JsonStringEnumMemberName("estimated-1rm")]
    Estimated1Rm,
    MaxWeight,
    MaxReps,
    MaxVolume,
}

internal enum RecordedBy
{
    Trainer,
    Client,
}
