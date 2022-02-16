using System.Reflection;
using App.ServiceInstallation;
using Infrastructure.Configuration;
using Shared.Extensions;

namespace App.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="IServiceCollection"/> interface.
/// </summary>
internal static class ServiceCollectionExtensions
{
    /// <summary>
    /// Installs the services using the <see cref="IServiceInstaller"/> implementations defined in the specified assembly.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for service installers implementations.</param>
    internal static void InstallServicesFromAssembly(this IServiceCollection services, Assembly assembly) =>
        InstanceFactory
            .CreateFromAssemblies<IServiceInstaller>(assembly)
            .ForEach(serviceInstaller => serviceInstaller.Install(services));
}
