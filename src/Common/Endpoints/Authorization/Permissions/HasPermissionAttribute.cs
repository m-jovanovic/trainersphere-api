using Microsoft.AspNetCore.Authorization;

namespace Endpoints.Authorization.Permissions;

/// <summary>
/// Specifies that the method that this attribute is applied to requires the specified permission.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HasPermissionAttribute"/> class.
    /// </summary>
    /// <param name="permission">The permission.</param>
    public HasPermissionAttribute(string permission)
        : base(permission)
    {
    }
}
