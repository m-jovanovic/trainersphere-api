namespace Modules.Users.Endpoints.Contracts.Users;

/// <summary>
/// Represents the user's details request.
/// </summary>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record UpdateUserDetailsRequest(string FirstName, string LastName);
