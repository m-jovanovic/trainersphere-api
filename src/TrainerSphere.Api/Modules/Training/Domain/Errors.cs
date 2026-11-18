using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Domain;

internal static class ClientErrors
{
    public static ForbiddenError ListForbidden { get; } = new("Client.ListForbidden", "Only trainers can list clients.");

    public static NotFoundError NotFound(Guid id) => new("Client.NotFound", $"The client '{id}' was not found.");

    public static DomainError Archived(Guid id) => new("Client.Archived", $"The client '{id}' is archived.");
}

internal static class ExerciseErrors
{
    public static ForbiddenError NotEditable { get; } = new("Exercise.NotEditable", "Library exercises cannot be changed.");

    public static NotFoundError NotFound(Guid id) => new("Exercise.NotFound", $"The exercise '{id}' was not found.");

    public static DomainError Archived(Guid id) => new("Exercise.Archived", $"The exercise '{id}' is archived.");

    public static ConflictError NameInUse(string name) => new("Exercise.NameInUse", $"You already have an exercise named '{name}'.");
}

internal static class WorkoutErrors
{
    public static NotFoundError NotFound(Guid id) => new("Workout.NotFound", $"The workout '{id}' was not found.");

    public static DomainError Archived(Guid id) => new("Workout.Archived", $"The workout '{id}' is archived.");
}

internal static class ProgramErrors
{
    public static NotFoundError NotFound(Guid id) => new("Program.NotFound", $"The program '{id}' was not found.");

    public static DomainError Archived(Guid id) => new("Program.Archived", $"The program '{id}' is archived.");

    public static DomainError SlotOutOfRange(int week, int day, int weeks) =>
        new("Program.SlotOutOfRange", $"The slot week {week}, day {day} is outside the program grid of {weeks} week(s) x 7 days.");

    public static DomainError TooManySlots(int week, int day, int max) =>
        new("Program.TooManySlots", $"Week {week}, day {day} has more than {max} workouts.");

    public static NotFoundError AssignmentNotFound(Guid id) =>
        new("ProgramAssignment.NotFound", $"The program assignment '{id}' was not found.");
}

internal static class AssignmentErrors
{
    public static NotFoundError NotFound(Guid id) => new("Assignment.NotFound", $"The assignment '{id}' was not found.");

    public static ConflictError NotScheduled(AssignmentStatus status) =>
        new("Assignment.NotScheduled", $"The assignment is {status.ToString().ToLowerInvariant()}, not scheduled.");

    public static ConflictError HasSession(Guid sessionId) =>
        new("Assignment.NotScheduled", "A session was already started from this assignment.")
        {
            Extensions = new Dictionary<string, object?> { ["sessionId"] = sessionId },
        };
}

internal static class SessionErrors
{
    public static ConflictError Completed { get; } = new("Session.Completed", "The session is completed and can no longer be changed.");

    public static NotFoundError NotFound(Guid id) => new("Session.NotFound", $"The session '{id}' was not found.");

    public static ConflictError AlreadyStartedForAssignment(Guid sessionId) =>
        new("Session.AlreadyStartedForAssignment", "A session was already started for this assignment.")
        {
            Extensions = new Dictionary<string, object?> { ["sessionId"] = sessionId },
        };

    public static ConflictError IdInUse(Guid id) => new("Session.IdInUse", $"The id '{id}' is already used by another session.");

    public static ConflictError SetIdInUse(Guid id) => new("Set.IdInUse", $"The id '{id}' is already used by a set of another session.");
}
