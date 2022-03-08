using Microsoft.AspNetCore.Authorization;

namespace Modules.Users.Endpoints.Attributes;

/// <summary>
/// Specifies that the method that this attribute is applied to requires that the current user
/// has to be the same user that is being accessed.
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false)]
public sealed class SameUserAttribute : AuthorizeAttribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SameUserAttribute"/> class.
    /// </summary>
    public SameUserAttribute()
        : base("SameUser")
    {
    }
}
