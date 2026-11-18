using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Domain;

internal enum InvitationStatus
{
    Pending,
    Accepted,
    Cancelled,
    Expired,
}

/// <summary>
/// A trainer's invitation for a future client. Only Pending, Accepted and Cancelled are stored; Expired is a
/// pending invitation past its expiry and is derived at read time.
/// </summary>
internal sealed class Invitation
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(7);

    private Invitation(InvitationRecord record)
    {
        Id = record.Id;
        TrainerId = record.TrainerId;
        Email = record.Email;
        FirstName = record.FirstName;
        LastName = record.LastName;
        Message = record.Message;
        TokenHash = record.TokenHash;
        StoredStatus = Enum.Parse<InvitationStatus>(record.Status);
        CreatedAt = record.CreatedAt;
        ExpiresAt = record.ExpiresAt;
        AcceptedAt = record.AcceptedAt;
        CancelledAt = record.CancelledAt;
        ClientId = record.ClientId;
    }

    public Guid Id { get; }

    public Guid TrainerId { get; }

    public string Email { get; }

    public string FirstName { get; }

    public string LastName { get; }

    public string? Message { get; }

    public string TokenHash { get; private set; }

    public InvitationStatus StoredStatus { get; private set; }

    public DateTime CreatedAt { get; }

    public DateTime ExpiresAt { get; private set; }

    public DateTime? AcceptedAt { get; private set; }

    public DateTime? CancelledAt { get; private set; }

    public Guid? ClientId { get; private set; }

    public static Invitation Issue(Guid id, Guid trainerId, string email, string firstName, string lastName, string? message, string tokenHash, DateTime now) =>
        new(new InvitationRecord
        {
            Id = id,
            TrainerId = trainerId,
            Email = email,
            FirstName = firstName.Trim(),
            LastName = lastName.Trim(),
            Message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            TokenHash = tokenHash,
            Status = nameof(InvitationStatus.Pending),
            CreatedAt = now,
            ExpiresAt = now + Lifetime,
        });

    public static Invitation Restore(InvitationRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        return new Invitation(record);
    }

    public InvitationStatus StatusAt(DateTime now) =>
        StoredStatus == InvitationStatus.Pending && now >= ExpiresAt ? InvitationStatus.Expired : StoredStatus;

    /// <summary>
    /// Issues a new link and restarts the expiry. Expired invitations can be re-sent; accepted or cancelled ones cannot.
    /// </summary>
    public Result<Invitation> Resend(string tokenHash, DateTime now)
    {
        if (StoredStatus != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPending(StatusAt(now));
        }

        TokenHash = tokenHash;
        ExpiresAt = now + Lifetime;
        return this;
    }

    public Result<Unit> Cancel(DateTime now)
    {
        switch (StoredStatus)
        {
            case InvitationStatus.Accepted:
                return InvitationErrors.NotPending(InvitationStatus.Accepted);
            case InvitationStatus.Pending:
                StoredStatus = InvitationStatus.Cancelled;
                CancelledAt = now;
                break;
        }

        return Result.Success();
    }

    public Result<Invitation> Accept(Guid clientId, DateTime now)
    {
        InvitationStatus status = StatusAt(now);

        if (status != InvitationStatus.Pending)
        {
            return InvitationErrors.NotPending(status);
        }

        StoredStatus = InvitationStatus.Accepted;
        AcceptedAt = now;
        ClientId = clientId;
        return this;
    }
}

internal sealed record InvitationRecord
{
    public Guid Id { get; init; }

    public Guid TrainerId { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Message { get; init; }

    public string TokenHash { get; init; } = string.Empty;

    public string Status { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? AcceptedAt { get; init; }

    public DateTime? CancelledAt { get; init; }

    public Guid? ClientId { get; init; }
}
