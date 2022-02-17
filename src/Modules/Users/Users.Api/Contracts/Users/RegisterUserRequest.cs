namespace Users.Api.Contracts.Users;

public sealed record RegisterUserRequest(string FirstName, string LastName, string Email);
