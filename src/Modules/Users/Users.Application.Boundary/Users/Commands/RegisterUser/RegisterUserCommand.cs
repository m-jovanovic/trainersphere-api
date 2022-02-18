using Application.Messaging;

namespace Users.Application.Boundary.Users.Commands.RegisterUser;

public sealed record RegisterUserCommand(string IdentityProviderId, string FirstName, string LastName, string Email) : ICommand;
