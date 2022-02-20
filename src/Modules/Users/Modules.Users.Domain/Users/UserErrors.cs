using Domain.Results;

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
    public static Error EmailIsNotUnique => new("User.EmailIsNotUnique", "The user email is already in use.");
}
