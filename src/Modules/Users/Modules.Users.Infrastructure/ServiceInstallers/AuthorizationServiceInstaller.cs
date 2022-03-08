using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Infrastructure.ServiceInstallers;

/// <summary>
/// Represents the users module authorization service installer.
/// </summary>
internal sealed class AuthorizationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services.ConfigureOptions<AuthorizationOptionsSetup>();
}
