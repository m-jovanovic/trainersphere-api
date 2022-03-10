using Shared.Results;

namespace Modules.Training.Domain.Trainers;

/// <summary>
/// Represents the trainer repository interface.
/// </summary>
public interface ITrainerRepository
{
    /// <summary>
    /// Gets the trainer with the specified identifier, if it exists.
    /// </summary>
    /// <param name="id">The trainer identifier.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The trainer with the specified identifier if it exists, otherwise null.</returns>
    Task<Result<Trainer>> GetByIdAsync(TrainerId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified trainer to the repository.
    /// </summary>
    /// <param name="trainer">The trainer.</param>
    void Add(Trainer trainer);
}
