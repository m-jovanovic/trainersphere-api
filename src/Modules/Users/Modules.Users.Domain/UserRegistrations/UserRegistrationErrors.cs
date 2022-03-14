using Shared.Results;

namespace Modules.Users.Domain.UserRegistrations;

/// <summary>
/// Contains the user registrations errors.
/// </summary>
public static class UserRegistrationErrors
{
    /// <summary>
    /// Gets the pending exists error.
    /// </summary>
    public static Error PendingExists => new(
        "UserRegistration.PendingExists",
        "There is already an existing user registration for the specified email.");
}
