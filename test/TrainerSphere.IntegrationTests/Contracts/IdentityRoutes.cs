namespace TrainerSphere.IntegrationTests.Contracts;

/// <summary>
/// Identity routes exactly as the HTTP contract documents them (sections 4 and 5).
/// </summary>
public static class IdentityRoutes
{
    public const string Signup = "/api/auth/signup";
    public const string Login = "/api/auth/login";
    public const string Refresh = "/api/auth/refresh";
    public const string Logout = "/api/auth/logout";
    public const string Me = "/api/auth/me";
    public const string ChangePassword = "/api/auth/change-password";
    public const string ForgotPassword = "/api/auth/forgot-password";
    public const string ResetPassword = "/api/auth/reset-password";
    public const string Invitations = "/api/invitations";

    public static readonly string[] UserProperties = ["id", "email", "firstName", "lastName", "role", "trainerId", "units", "timeZone", "createdAt"];

    public static readonly string[] AuthResponseProperties = ["accessToken", "tokenType", "expiresIn", "user"];

    public static readonly string[] InvitationProperties =
        ["id", "email", "firstName", "lastName", "message", "status", "createdAt", "expiresAt", "acceptedAt", "clientId"];

    public static readonly string[] InvitationPreviewProperties = ["email", "firstName", "lastName", "trainerName", "message", "status", "expiresAt"];

    public static readonly string[] PagedProperties = ["items", "page", "pageSize", "totalCount", "totalPages"];

    public static string Invitation(Guid id) => $"{Invitations}/{id}";

    public static string Resend(Guid id) => $"{Invitations}/{id}/resend";

    public static string Preview(string token) => $"{Invitations}/{token}";

    public static string Accept(string token) => $"{Invitations}/{token}/accept";
}
