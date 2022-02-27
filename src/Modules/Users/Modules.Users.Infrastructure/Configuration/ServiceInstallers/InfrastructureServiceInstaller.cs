using Application.Time;
using Infrastructure.Configuration;
using Infrastructure.Time;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the user's module infrastructure service installer.
/// </summary>
internal sealed class InfrastructureServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) => services.TryAddTransient<ISystemTime, SystemTime>();
}
