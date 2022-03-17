using Shared.Results;

namespace Modules.Training.Domain.Invitations;

/// <summary>
/// Contains the invitation errors.
/// </summary>
public static class InvitationErrors
{
    /// <summary>
    /// Gets the pending exists error.
    /// </summary>
    public static Error PendingExists => new("Invitation.PendingExists", "There is already an existing invitation for the client.");

    /// <summary>
    /// Gets the not found error.
    /// </summary>
    public static Func<InvitationId, Error> NotFound => invitationId => new Error(
        "Invitation.NotFound",
        $"The invitation with the identifier '{invitationId.Value}' was not found.");

    /// <summary>
    /// Gets the expired error.
    /// </summary>
    public static Func<InvitationId, Error> Expired => invitationId => new Error(
        "Invitation.Expired",
        $"The invitation with the identifier '{invitationId.Value}' has expired.");

    /// <summary>
    /// Gets the cancelled error.
    /// </summary>
    public static Func<InvitationId, Error> Cancelled => invitationId => new Error(
        "Invitation.Cancelled",
        $"The invitation with the identifier '{invitationId.Value}' has been cancelled.");
}
