using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Domain;

/// <summary>
/// A client as seen by Training: a projection of the Identity account (names, email, time zone) plus the coaching
/// data the trainer keeps. The id equals the user id.
/// </summary>
internal sealed class ClientProfile
{
    public const int NotesMaxLength = 2000;

    private ClientProfile(ClientRecord record)
    {
        Id = record.Id;
        TrainerId = record.TrainerId;
        Email = record.Email;
        FirstName = record.FirstName;
        LastName = record.LastName;
        TimeZone = record.TimeZone;
        Status = Enum.Parse<ClientStatus>(record.Status);
        JoinedAt = record.JoinedAt;
        ArchivedAt = record.ArchivedAt;
        Goals = record.Goals;
        TrainerNotes = record.TrainerNotes;
        UpdatedAt = record.UpdatedAt;
    }

    public Guid Id { get; }

    public Guid TrainerId { get; }

    public string Email { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string TimeZone { get; }

    public ClientStatus Status { get; private set; }

    public DateTime JoinedAt { get; }

    public DateTime? ArchivedAt { get; private set; }

    public string? Goals { get; private set; }

    public string? TrainerNotes { get; private set; }

    public DateTime UpdatedAt { get; private set; }

    public bool IsArchived => Status == ClientStatus.Archived;

    public static ClientProfile Restore(ClientRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new ClientProfile(record);
    }

    /// <summary>
    /// A trainer sees their own clients; a client sees only themself.
    /// </summary>
    public bool IsAccessibleBy(Viewer viewer) => viewer.IsTrainer ? TrainerId == viewer.UserId : Id == viewer.UserId;

    public Result<ClientProfile> EnsureActive() => IsArchived ? ClientErrors.Archived(Id) : this;

    public void UpdateNotes(string? goals, string? trainerNotes, DateTime now)
    {
        Goals = Text.NullIfBlank(goals);
        TrainerNotes = Text.NullIfBlank(trainerNotes);
        UpdatedAt = now;
    }

    /// <summary>
    /// Archives the client; returns false when it already was archived.
    /// </summary>
    public bool Archive(DateTime now)
    {
        if (IsArchived)
        {
            return false;
        }

        Status = ClientStatus.Archived;
        ArchivedAt = now;
        UpdatedAt = now;
        return true;
    }

    public void Unarchive(DateTime now)
    {
        if (!IsArchived)
        {
            return;
        }

        Status = ClientStatus.Active;
        ArchivedAt = null;
        UpdatedAt = now;
    }
}

internal sealed record ClientRecord
{
    public Guid Id { get; init; }

    public Guid TrainerId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string TimeZone { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime JoinedAt { get; init; }

    public DateTime? ArchivedAt { get; init; }

    public string? Goals { get; init; }

    public string? TrainerNotes { get; init; }

    public DateTime UpdatedAt { get; init; }
}

internal static class Text
{
    public static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
