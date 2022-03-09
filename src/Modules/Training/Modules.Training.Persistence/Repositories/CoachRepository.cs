using Application.ServiceLifetimes;
using Modules.Training.Domain.Coaches;

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

    public void Add(Coach coach) => _dbContext.Set<Coach>().Add(coach);
}
