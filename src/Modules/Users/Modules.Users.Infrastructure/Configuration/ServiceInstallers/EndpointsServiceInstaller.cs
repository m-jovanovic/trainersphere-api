using Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the user's module endpoints service installer.
/// </summary>
internal sealed class EndpointsServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .AddControllers()
            .AddApplicationPart(Endpoints.AssemblyReference.Assembly);
}
