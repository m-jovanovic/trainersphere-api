using Shared.Results;

namespace Modules.Training.Domain.Coaches;

/// <summary>
/// Represents the coach repository interface.
/// </summary>
public interface ICoachRepository
{
    /// <summary>
    /// Gets the coach with the specified identifier, if it exists.
    /// </summary>
    /// <param name="id">The coach identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The coach with the specified identifier if it exists, otherwise null.</returns>
    Task<Result<Coach>> GetByIdAsync(CoachId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified coach to the repository.
    /// </summary>
    /// <param name="coach">The coach.</param>
    void Add(Coach coach);
}
