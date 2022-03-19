using Shared.Errors;

namespace Modules.Users.Domain.UserRegistrations;

/// <summary>
/// Contains the user registrations errors.
/// </summary>
public static class UserRegistrationErrors
{
    /// <summary>
    /// Gets the identity not found error.
    /// </summary>
    public static Error IdentityNotFound => new("UserRegistration.IdentityNotFound", "The user registration identity was not found.");

    /// <summary>
    /// Gets the pending exists error.
    /// </summary>
    public static Error PendingExists => new(
        "UserRegistration.PendingExists",
        "There is already an existing user registration for the specified email.");

    /// <summary>
    /// Gets the email does not match error.
    /// </summary>
    public static Error EmailDoesNotMatch => new(
        "UserRegistration.EmailDoesNotMatch",
        "There specified email does not match the user registration's email.");

    /// <summary>
    /// Gets the not found error.
    /// </summary>
    public static Func<UserRegistrationId, Error> NotFound => userRegistrationId => new(
        "UserRegistration.NotFound",
        $"The user registration with the identifier '{userRegistrationId.Value}' was not found.");

    /// <summary>
    /// Gets the confirmed error.
    /// </summary>
    public static Func<UserRegistrationId, Error> Confirmed => userRegistrationId => new(
        "UserRegistration.Confirmed",
        $"The user registration with the identifier '{userRegistrationId.Value}' has been confirmed.");

    /// <summary>
    /// Gets the cancelled error.
    /// </summary>
    public static Func<UserRegistrationId, Error> Cancelled => userRegistrationId => new(
        "UserRegistration.Cancelled",
        $"The user registration with the identifier '{userRegistrationId.Value}' has been cancelled.");

    /// <summary>
    /// Gets the expired error.
    /// </summary>
    public static Func<UserRegistrationId, Error> Expired => userRegistrationId => new(
        "UserRegistration.Expired",
        $"The user registration with the identifier '{userRegistrationId.Value}' has expired.");
}
