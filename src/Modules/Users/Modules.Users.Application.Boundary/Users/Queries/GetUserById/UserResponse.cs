namespace Modules.Users.Application.Boundary.Users.Queries.GetUserById;

public sealed record UserResponse(Guid Id, string Email, string FirstName, string LastName);
