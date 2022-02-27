using System;

namespace Modules.Users.Domain.UnitTests.Data.Users;

public sealed record CreateUserRequest(Guid Id, string IdentityProviderId, string Email, string FirstName, string LastName);
