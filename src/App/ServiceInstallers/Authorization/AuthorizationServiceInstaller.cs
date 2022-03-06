using Endpoints.Authorization.Permissions;
using Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;

namespace App.ServiceInstallers.Authorization;

/// <summary>
/// Represents the authorization service installer.
/// </summary>
internal sealed class AuthorizationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .AddAuthorization()
            .ConfigureOptions<AuthorizationOptionsSetup>()
            .AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>()
            .AddSingleton<IAuthorizationPolicyProvider, PermissionAuthorizationPolicyProvider>();
}
