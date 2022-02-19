using Modules.Users.Domain.Users;

namespace Modules.Users.Persistence.Repositories;

/// <summary>
/// Represents the user repository.
/// </summary>
internal sealed class UserRepository : IUserRepository
{
    private readonly UsersDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UserRepository(UsersDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public void Add(User user) => _dbContext.Set<User>().Add(user);
}
