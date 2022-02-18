namespace Users.Api.Contracts.Users;

public sealed record RegisterUserRequest(string IdentityProviderId, string FirstName, string LastName, string Email);
