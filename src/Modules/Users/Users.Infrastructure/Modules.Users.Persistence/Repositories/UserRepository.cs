using Modules.Users.Domain.Users;

namespace Modules.Users.Persistence.Repositories;

/// <summary>
/// Represents the user repository.
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    public void Add(User user) => throw new NotImplementedException();
}
