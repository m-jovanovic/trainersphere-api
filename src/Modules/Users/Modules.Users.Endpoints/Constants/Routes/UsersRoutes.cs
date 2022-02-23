namespace Modules.Users.Endpoints.Constants.Routes;

internal static class UsersRoutes
{
    internal const string Tag = "Users";

    internal const string BaseUri = "users";

    internal const string Register = $"{BaseUri}/register";

    internal const string GetById = $"{BaseUri}/{{userId:guid}}";
}
