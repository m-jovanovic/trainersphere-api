using Infrastructure.Configuration;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Infrastructure.Authorization.SameUser;

namespace Modules.Users.Infrastructure.ServiceInstallers;

/// <summary>
/// Represents the users module authorization service installer.
/// </summary>
internal sealed class AuthorizationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .AddHttpContextAccessor()
            .ConfigureOptions<AuthorizationOptionsSetup>()
            .AddSingleton<IAuthorizationHandler, SameUserAuthorizationHandler>();
}
