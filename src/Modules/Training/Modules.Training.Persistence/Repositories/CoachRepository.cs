using Application.ServiceLifetimes;
using Microsoft.EntityFrameworkCore;
using Modules.Training.Domain.Coaches;
using Shared.Results;

namespace Modules.Training.Persistence.Repositories;

/// <summary>
/// Represents the coach repository.
/// </summary>
internal sealed class CoachRepository : ICoachRepository, IScoped
{
    private readonly TrainingDbContext _dbContext;

    /// <summary>
    /// Initializes a new instance of the <see cref="CoachRepository"/> class.
    /// </summary>
    /// <param name="dbContext">The database context.</param>
    public CoachRepository(TrainingDbContext dbContext) => _dbContext = dbContext;

    /// <inheritdoc />
    public async Task<Result<Coach>> GetByIdAsync(CoachId id, CancellationToken cancellationToken = default) =>
        Result.Create(await _dbContext.Set<Coach>().FirstOrDefaultAsync(user => user.Id == id, cancellationToken))
            .MapFailure(() => CoachErrors.NotFound(id));

    /// <inheritdoc />
    public void Add(Coach coach) => _dbContext.Set<Coach>().Add(coach);
}
