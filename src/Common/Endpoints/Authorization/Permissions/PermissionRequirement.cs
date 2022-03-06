using Microsoft.AspNetCore.Authorization;

namespace Endpoints.Authorization.Permissions;

/// <summary>
/// Represents the permission authorization requirement.
/// </summary>
public sealed class PermissionRequirement : IAuthorizationRequirement
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionRequirement"/> class.
    /// </summary>
    /// <param name="permission">The permission.</param>
    public PermissionRequirement(string permission) => Permission = permission;

    /// <summary>
    /// Gets the permission.
    /// </summary>
    public string Permission { get; }
}
