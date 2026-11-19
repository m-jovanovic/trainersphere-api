using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Domain;

internal static class AuthErrors
{
    public static UnauthorizedError InvalidCredentials { get; } = new("Auth.InvalidCredentials", "The email or password is incorrect.");

    public static UnauthorizedError InvalidRefreshToken { get; } = new("Auth.InvalidRefreshToken", "The session has expired. Sign in again.");

    public static ValidationError InvalidResetToken { get; } =
        ValidationError.For("token", "The reset link is invalid or has expired.", "Auth.InvalidResetToken");

    public static ValidationError InvalidCurrentPassword { get; } =
        ValidationError.For("currentPassword", "The current password is incorrect.", "Auth.InvalidCurrentPassword");
}

internal static class UserErrors
{
    public static ConflictError EmailInUse(string email) => new("User.EmailInUse", $"An account with the email '{email}' already exists.");

    public static NotFoundError NotFound(Guid id) => new("User.NotFound", $"The user '{id}' was not found.");
}

internal static class InvitationErrors
{
    public static NotFoundError NotFound { get; } = new("Invitation.NotFound", "The invitation was not found.");

    public static ConflictError PendingExists(string email) =>
        new("Invitation.PendingExists", $"There is already a pending invitation for '{email}'.");

    public static ConflictError NotPending(InvitationStatus status) =>
        new("Invitation.NotPending", $"The invitation is {status.ToString().ToLowerInvariant()}.");
}
