using Application.Messaging;

namespace Modules.Users.Application.Boundary.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(string IdentityProviderId, string FirstName, string LastName, string Email) : ICommand;
