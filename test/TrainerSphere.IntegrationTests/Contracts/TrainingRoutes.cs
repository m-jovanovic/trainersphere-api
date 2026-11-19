namespace TrainerSphere.IntegrationTests.Contracts;

/// <summary>
/// Training routes (sections 6-14 of the HTTP contract).
/// </summary>
public static class TrainingRoutes
{
    public const string Exercises = "/api/exercises";
    public const string ExerciseTaxonomy = "/api/exercises/taxonomy";
    public const string Workouts = "/api/workouts";
    public const string Programs = "/api/programs";
    public const string Assignments = "/api/assignments";
    public const string Sessions = "/api/sessions";
    public const string Clients = "/api/clients";
    public const string TrainerDashboard = "/api/dashboard/trainer";
    public const string ClientDashboard = "/api/dashboard/client";

    public static string Exercise(Guid id) => $"{Exercises}/{id}";

    public static string Workout(Guid id) => $"{Workouts}/{id}";

    public static string WorkoutDuplicate(Guid id) => $"{Workouts}/{id}/duplicate";

    public static string Program(Guid id) => $"{Programs}/{id}";

    public static string ProgramAssign(Guid id) => $"{Programs}/{id}/assign";

    public static string ProgramAssignmentsOf(Guid clientId) => $"{Clients}/{clientId}/program-assignments";

    public static string ProgramAssignment(Guid id) => $"/api/program-assignments/{id}";

    public static string Assignment(Guid id) => $"{Assignments}/{id}";

    public static string AssignmentSkip(Guid id) => $"{Assignments}/{id}/skip";

    public static string Session(Guid id) => $"{Sessions}/{id}";

    public static string SessionSet(Guid id, Guid setId) => $"{Sessions}/{id}/sets/{setId}";

    public static string SessionComplete(Guid id) => $"{Sessions}/{id}/complete";

    public static string PersonalRecords(Guid clientId) => $"{Clients}/{clientId}/personal-records";

    public static string PersonalRecordHistory(Guid clientId) => $"{PersonalRecords(clientId)}/history";

    public static string ProgressExercises(Guid clientId) => $"{Clients}/{clientId}/progress/exercises";

    public static string ProgressExercise(Guid clientId, Guid exerciseId) => $"{ProgressExercises(clientId)}/{exerciseId}";

    public static string ProgressSummary(Guid clientId) => $"{Clients}/{clientId}/progress/summary";

    public static string BodyMetrics(Guid clientId) => $"{Clients}/{clientId}/body-metrics";

    public static string BodyMetric(Guid clientId, string date) => $"{BodyMetrics(clientId)}/{date}";

    public static string Client(Guid id) => $"{Clients}/{id}";

    public static string ClientArchive(Guid id) => $"{Clients}/{id}/archive";

    public static string ClientUnarchive(Guid id) => $"{Clients}/{id}/unarchive";
}
