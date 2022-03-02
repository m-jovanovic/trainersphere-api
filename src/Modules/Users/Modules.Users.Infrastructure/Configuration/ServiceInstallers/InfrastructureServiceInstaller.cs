using Application.Time;
using Infrastructure.BackgroundJobs;
using Infrastructure.Configuration;
using Infrastructure.Time;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scrutor;
using Shared.Extensions;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the user's module infrastructure service installer.
/// </summary>
internal sealed class InfrastructureServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .Tap(services.TryAddTransient<ISystemTime, SystemTime>)
            .Tap(AddRecurringJobConfigurations);

    private static void AddRecurringJobConfigurations(IServiceCollection services) =>
        services.Scan(scan =>
            scan.FromAssemblies(AssemblyReference.Assembly)
                .AddClasses(filter => filter.Where(type => type.IsAssignableFrom(typeof(IRecurringJobConfiguration))), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Append)
                .AsImplementedInterfaces()
                .WithTransientLifetime());
}
