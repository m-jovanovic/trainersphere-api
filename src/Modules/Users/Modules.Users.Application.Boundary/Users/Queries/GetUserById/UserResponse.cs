namespace Modules.Users.Application.Boundary.Users.Queries.GetUserById;

/// <summary>
/// Represents the user response.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="Email">The email.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName);
