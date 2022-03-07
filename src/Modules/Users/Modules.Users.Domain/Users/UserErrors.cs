using Shared.Results;

namespace Modules.Users.Domain.Users;

/// <summary>
/// Contains the users errors.
/// </summary>
public static class UserErrors
{
    /// <summary>
    /// Gets the identity not found error.
    /// </summary>
    public static Error IdentityNotFound => new("User.IdentityNotFound", "The user identity was not found.");

    /// <summary>
    /// Gets the email is not unique error.
    /// </summary>
    public static Error EmailIsNotUnique => new("User.EmailIsNotUnique", "The specified email is already in use.");

    /// <summary>
    /// Gets the not found error.
    /// </summary>
    public static Func<UserId, Error> NotFound => userId => new Error(
        "User.NotFound",
        $"The user with the identifier '{userId.Value}' was not found.");

    /// <summary>
    /// Gets the not found by identity error.
    /// </summary>
    public static Func<string, Error> NotFoundByIdentity =>
        identityProviderId => new Error(
            "User.NotFoundByIdentity",
            $"The user with the identity provider identifier '{identityProviderId}' was not found.");
}
