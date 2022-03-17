using Application.Contracts.Messaging;

namespace Modules.Training.Application.Boundary.Invitations.Commands.CancelInvitation;

/// <summary>
/// Represents the command for cancelling an invitation.
/// </summary>
/// <param name="InvitationId">The invitation identifier.</param>
public sealed record CancelInvitationCommand(Guid InvitationId) : ICommand;
