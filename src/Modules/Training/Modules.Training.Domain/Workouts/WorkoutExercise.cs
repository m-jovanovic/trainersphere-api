namespace Modules.Training.Domain.Workouts;

/// <summary>
/// Represents the workout exercise entity.
/// </summary>
public sealed class WorkoutExercise
{
    /// <summary>
    /// Initializes a new instance of the <see cref="WorkoutExercise"/> class.
    /// </summary>
    /// <param name="workoutId">The workout identifier.</param>
    /// <param name="exerciseId">The exercise identifier.</param>
    /// <param name="goal">The goal.</param>
    /// <param name="restInterval">The rest interval.</param>
    internal WorkoutExercise(WorkoutId workoutId, ExerciseId exerciseId, Goal goal, RestInterval restInterval)
    {
        WorkoutId = workoutId;
        ExerciseId = exerciseId;
        RestInterval = restInterval;
        Goal = goal;
    }

    /// <summary>
    /// Gets the workout identifier.
    /// </summary>
    public WorkoutId WorkoutId { get; private set; }

    /// <summary>
    /// Gets the exercise identifier.
    /// </summary>
    public ExerciseId ExerciseId { get; private set; }

    /// <summary>
    /// Gets the goal.
    /// </summary>
    public Goal Goal { get; private set; }

    /// <summary>
    /// Gets the rest interval.
    /// </summary>
    public RestInterval RestInterval { get; private set; }
}
