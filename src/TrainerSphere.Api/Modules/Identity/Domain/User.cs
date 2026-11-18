using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Identity.Domain;

internal enum UserRole
{
    Trainer,
    Client,
}

internal enum Units
{
    Kg,
    Lb,
}

internal sealed class User
{
    private User(Guid id, string email, string passwordHash, string firstName, string lastName, UserRole role, Guid? trainerId, Units units, string timeZone, DateTime createdAt)
    {
        Id = id;
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        Role = role;
        TrainerId = trainerId;
        Units = units;
        TimeZone = timeZone;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; }

    public string Email { get; }

    public string PasswordHash { get; private set; }

    public string FirstName { get; private set; }

    public string LastName { get; private set; }

    public UserRole Role { get; }

    public Guid? TrainerId { get; }

    public Units Units { get; private set; }

    public string TimeZone { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime UpdatedAt { get; private set; }

    public string FullName => $"{FirstName} {LastName}";

    public static User RegisterTrainer(Guid id, string email, string passwordHash, string firstName, string lastName, string? timeZone, DateTime now) =>
        new(id, email, passwordHash, firstName.Trim(), lastName.Trim(), UserRole.Trainer, null, Units.Kg, timeZone ?? TimeZones.Utc, now);

    public static User RegisterClient(Guid id, Guid trainerId, string email, string passwordHash, string firstName, string lastName, string? timeZone, DateTime now) =>
        new(id, email, passwordHash, firstName.Trim(), lastName.Trim(), UserRole.Client, trainerId, Units.Kg, timeZone ?? TimeZones.Utc, now);

    public static User Restore(UserRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new User(
            record.Id,
            record.Email,
            record.PasswordHash,
            record.FirstName,
            record.LastName,
            Enum.Parse<UserRole>(record.Role),
            record.TrainerId,
            Enum.Parse<Units>(record.Units),
            record.TimeZone,
            record.CreatedAt)
        {
            UpdatedAt = record.UpdatedAt,
        };
    }

    public void UpdateProfile(string firstName, string lastName, Units units, string timeZone, DateTime now)
    {
        FirstName = firstName.Trim();
        LastName = lastName.Trim();
        Units = units;
        TimeZone = timeZone;
        UpdatedAt = now;
    }

    public void ChangePasswordHash(string passwordHash, DateTime now)
    {
        PasswordHash = passwordHash;
        UpdatedAt = now;
    }
}

/// <summary>
/// The persisted shape of a user, as read by Dapper.
/// </summary>
internal sealed record UserRecord
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string PasswordHash { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string Role { get; init; } = string.Empty;

    public Guid? TrainerId { get; init; }

    public string Units { get; init; } = string.Empty;

    public string TimeZone { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
