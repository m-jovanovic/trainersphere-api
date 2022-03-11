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
}
