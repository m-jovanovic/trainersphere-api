using Domain.Results;
using Microsoft.EntityFrameworkCore;
using Modules.Users.Domain.Users;
using Shared.ServiceLifetimes;

namespace Modules.Users.Persistence.Repositories;

/// <summary>
/// Represents the user repository.
/// </summary>
internal sealed class UserRepository : IUserRepository, IScoped
{
    private readonly UsersDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public UserRepository(UsersDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Result<User>> GetByIdAsync(UserId id, CancellationToken cancellationToken = default) =>
        Result.Create(await _dbContext.Set<User>().FirstOrDefaultAsync(user => user.Id == id, cancellationToken))
            .MapFailure(() => UserErrors.NotFound(id));

    /// <inheritdoc />
    public async Task<Result> IsEmailUniqueAsync(string email, CancellationToken cancellationToken = default) =>
        !await _dbContext.Set<User>().AnyAsync(user => user.Email == email, cancellationToken) ?
            Result.Success() :
            Result.Failure(UserErrors.EmailIsNotUnique);

    /// <inheritdoc />
    public void Add(User user) => _dbContext.Set<User>().Add(user);
}
