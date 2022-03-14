using Domain.Primitives;
using Domain.Time;
using Modules.Training.Domain.Invitations.Events;
using Modules.Training.Domain.Trainers;
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
    /// <param name="trainerId">The trainer identifier.</param>
    /// <param name="email">The email.</param>
    /// <param name="sender">The sender.</param>
    /// <param name="status">The status.</param>
    private Invitation(InvitationId id, TrainerId trainerId, string email, Sender sender, InvitationStatus status)
        : base(id)
    {
        TrainerId = trainerId;
        Email = email;
        Sender = sender;
        Status = status;
    }

    /// <summary>
    /// Gets the trainer identifier.
    /// </summary>
    public TrainerId TrainerId { get; private set; }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// Gets the sender.
    /// </summary>
    public Sender Sender { get; private set; }

    /// <summary>
    /// Gets the status.
    /// </summary>
    public InvitationStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; private set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new invitation for the specified trainer and the client with the specified email.
    /// </summary>
    /// <param name="trainer">The trainer.</param>
    /// <param name="email">The email.</param>
    /// <returns>The new invitation instance.</returns>
    internal static Result<Invitation> Create(Trainer trainer, string email)
    {
        var invitation = new Invitation(
            new InvitationId(Guid.NewGuid()),
            trainer.Id,
            email,
            new Sender(trainer.FirstName, trainer.LastName),
            InvitationStatus.Pending);

        invitation.RaiseDomainEvent(new InvitationCreatedDomainEvent(
            Guid.NewGuid(),
            SystemTimeProvider.UtcNow(),
            invitation.Id,
            invitation.TrainerId,
            invitation.Email));

        return invitation;
    }
}
