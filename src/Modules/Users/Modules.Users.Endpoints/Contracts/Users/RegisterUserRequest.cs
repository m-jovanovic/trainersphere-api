namespace Modules.Users.Endpoints.Contracts.Users;

public sealed record RegisterUserRequest(string IdentityProviderId, string FirstName, string LastName, string Email);
