namespace Modules.Training.Domain.Coaches;

/// <summary>
/// Represents the coach repository interface.
/// </summary>
public interface ICoachRepository
{
    /// <summary>
    /// Adds the specified coach to the repository.
    /// </summary>
    /// <param name="coach">The coach.</param>
    void Add(Coach coach);
}
