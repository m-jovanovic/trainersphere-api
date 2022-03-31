using Domain.Primitives;

namespace Modules.Training.Domain.Exercises;

/// <summary>
/// Represents the exercise entity.
/// </summary>
public sealed class Exercise : Entity<ExerciseId>, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Exercise"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="name">The name.</param>
    public Exercise(ExerciseId id, string name)
        : base(id) =>
        Name = name;

    /// <summary>
    /// Gets the name.
    /// </summary>
    public string Name { get; private set; }

    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; private set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; private set; }
}
