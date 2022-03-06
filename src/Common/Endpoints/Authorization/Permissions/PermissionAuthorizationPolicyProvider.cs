using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Endpoints.Authorization.Permissions;

/// <summary>
/// Represents the permission authorization policy provider.
/// </summary>
public sealed class PermissionAuthorizationPolicyProvider : DefaultAuthorizationPolicyProvider
{
    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAuthorizationPolicyProvider"/> class.
    /// </summary>
    /// <param name="options">The authorization options.</param>
    public PermissionAuthorizationPolicyProvider(IOptions<AuthorizationOptions> options)
        : base(options)
    {
    }

    /// <inheritdoc />
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        AuthorizationPolicy? authorizationPolicy = await base.GetPolicyAsync(policyName);

        if (authorizationPolicy is not null)
        {
            return authorizationPolicy;
        }

        return new AuthorizationPolicyBuilder()
            .AddRequirements(new PermissionRequirement(policyName))
            .Build();
    }
}
