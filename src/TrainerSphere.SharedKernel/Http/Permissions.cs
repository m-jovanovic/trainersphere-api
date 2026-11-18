using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;

namespace TrainerSphere.SharedKernel.Http;

public static class Permissions
{
    public const string InvitationsManage = "invitations:manage";
    public const string ClientsManage = "clients:manage";
    public const string ClientsRead = "clients:read";
    public const string ExercisesRead = "exercises:read";
    public const string ExercisesManage = "exercises:manage";
    public const string WorkoutsManage = "workouts:manage";
    public const string ProgramsManage = "programs:manage";
    public const string AssignmentsRead = "assignments:read";
    public const string AssignmentsManage = "assignments:manage";
    public const string AssignmentsSkip = "assignments:skip";
    public const string SessionsRead = "sessions:read";
    public const string SessionsLog = "sessions:log";
    public const string ProgressRead = "progress:read";
    public const string BodyMetricsWrite = "body-metrics:write";
    public const string TrainerDashboard = "dashboard:trainer";
    public const string ClientDashboard = "dashboard:client";
    public const string Profile = "profile";

    public static IReadOnlyList<string> All { get; } =
    [
        InvitationsManage, ClientsManage, ClientsRead, ExercisesRead, ExercisesManage, WorkoutsManage, ProgramsManage,
        AssignmentsRead, AssignmentsManage, AssignmentsSkip, SessionsRead, SessionsLog, ProgressRead, BodyMetricsWrite,
        TrainerDashboard, ClientDashboard, Profile,
    ];
}

/// <summary>
/// Maps roles to permissions. Resource ownership (which clients a trainer sees) is checked by the handlers.
/// </summary>
public static class RolePermissions
{
    private static readonly HashSet<string> Trainer =
    [
        Permissions.InvitationsManage, Permissions.ClientsManage, Permissions.ClientsRead, Permissions.ExercisesRead,
        Permissions.ExercisesManage, Permissions.WorkoutsManage, Permissions.ProgramsManage, Permissions.AssignmentsRead,
        Permissions.AssignmentsManage, Permissions.AssignmentsSkip, Permissions.SessionsRead, Permissions.ProgressRead,
        Permissions.BodyMetricsWrite, Permissions.TrainerDashboard, Permissions.Profile,
    ];

    private static readonly HashSet<string> Client =
    [
        Permissions.ClientsRead, Permissions.ExercisesRead, Permissions.AssignmentsRead, Permissions.AssignmentsSkip,
        Permissions.SessionsRead, Permissions.SessionsLog, Permissions.ProgressRead, Permissions.BodyMetricsWrite,
        Permissions.ClientDashboard, Permissions.Profile,
    ];

    public static IReadOnlySet<string> For(string? role) => role switch
    {
        Roles.Trainer => Trainer,
        Roles.Client => Client,
        _ => new HashSet<string>(),
    };
}

public static class PermissionAuthorizationExtensions
{
    public static string PolicyName(string permission) => "permission:" + permission;

    public static AuthorizationBuilder AddPermissionPolicies(this AuthorizationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        foreach (string permission in Permissions.All)
        {
            builder.AddPolicy(PolicyName(permission), policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionRequirement(permission)));
        }

        return builder;
    }

    public static TBuilder RequirePermission<TBuilder>(this TBuilder builder, string permission)
        where TBuilder : IEndpointConventionBuilder =>
        builder.RequireAuthorization(PolicyName(permission));
}

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}

public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(requirement);

        string? role = context.User.FindFirst(ClaimNames.Role)?.Value;

        if (RolePermissions.For(role).Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
