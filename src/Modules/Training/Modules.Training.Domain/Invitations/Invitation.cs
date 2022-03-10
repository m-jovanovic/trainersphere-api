using Domain.Primitives;
using Domain.Time;
using Modules.Training.Domain.Coaches;
using Modules.Training.Domain.Invitations.Events;
using Shared.Results;

namespace Modules.Training.Domain.Invitations;

/// <summary>
/// Represents the invitation entity.
/// </summary>
public sealed class Invitation : Entity<InvitationId>, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Invitation"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="coachId">The coach identifier.</param>
    /// <param name="email">The email.</param>
    private Invitation(InvitationId id, CoachId coachId, string email)
        : base(id)
    {
        CoachId = coachId;
        Email = email;
    }

    public CoachId CoachId { get; private set; }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; private set; }

    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; private set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new invitation for the specified coach and the client with the specified email.
    /// </summary>
    /// <param name="coach">The coach.</param>
    /// <param name="email">The email.</param>
    /// <returns>The new invitation instance.</returns>
    internal static Result<Invitation> Create(Coach coach, string email)
    {
        var invitation = new Invitation(new InvitationId(Guid.NewGuid()), coach.Id, email);

        invitation.RaiseDomainEvent(new InvitationCreatedDomainEvent(
            Guid.NewGuid(),
            SystemTimeProvider.UtcNow(),
            invitation.Id,
            invitation.CoachId,
            invitation.Email));

        return invitation;
    }
}
