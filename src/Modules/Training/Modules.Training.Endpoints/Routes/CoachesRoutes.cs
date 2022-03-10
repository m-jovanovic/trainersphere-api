namespace Modules.Training.Endpoints.Routes;

internal static class CoachesRoutes
{
    internal const string Tag = "Coaches";

    internal const string BaseUri = "coaches";

    internal const string InviteClient = $"{BaseUri}/{{coachId:guid}}/invite";
}
