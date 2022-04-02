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
    /// <param name="restInterval">The rest interval.</param>
    public WorkoutExercise(WorkoutId workoutId, ExerciseId exerciseId, RestInterval restInterval)
    {
        WorkoutId = workoutId;
        ExerciseId = exerciseId;
        RestInterval = restInterval;
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
    /// Gets the rest interval.
    /// </summary>
    public RestInterval RestInterval { get; private set; }
}
