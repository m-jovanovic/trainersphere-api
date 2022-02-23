namespace Modules.Users.Endpoints.Contracts.Users;

/// <summary>
/// Represents the request for registering a new user.
/// </summary>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Email">The email.</param>
public sealed record RegisterUserRequest(string FirstName, string LastName, string Email);
