using Application.Messaging;

namespace Modules.Users.Application.Boundary.Users.Commands.RegisterUser;

/// <summary>
/// Represents the command for registering a new user.
/// </summary>
/// <param name="IdentityProviderId">The identity provider identifier.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
/// <param name="Email">The email.</param>
public sealed record RegisterUserCommand(string IdentityProviderId, string FirstName, string LastName, string Email) : ICommand<Guid>;
