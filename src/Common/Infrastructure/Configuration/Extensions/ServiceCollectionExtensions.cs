using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Shared.Extensions;

namespace Infrastructure.Configuration.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Installs the services using the <see cref="IServiceInstaller"/> implementations defined in the specified assembly.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for service installer implementations.</param>
    public static void InstallServicesFromAssembly(this IServiceCollection services, Assembly assembly) =>
        InstanceFactory
            .CreateFromAssemblies<IServiceInstaller>(assembly)
            .ForEach(serviceInstaller => serviceInstaller.Install(services));
}
