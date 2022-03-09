using Shared.Results;

namespace Modules.Training.Domain.Coaches;

/// <summary>
/// Contains the coaches errors.
/// </summary>
public static class CoachErrors
{
    /// <summary>
    /// Gets the not found error.
    /// </summary>
    public static Func<CoachId, Error> NotFound => coachId => new Error(
        "Coach.NotFound",
        $"The coach with the identifier '{coachId.Value}' was not found.");
}
