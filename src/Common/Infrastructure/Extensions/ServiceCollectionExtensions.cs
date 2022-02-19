using System.Reflection;
using Infrastructure.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Scrutor;
using Shared.Extensions;
using Shared.ServiceLifetimes;

namespace Infrastructure.Extensions;

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
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection InstallServicesFromAssembly(this IServiceCollection services, Assembly assembly) =>
        services.Tap(
            () => InstanceFactory
                .CreateFromAssemblies<IServiceInstaller>(assembly)
                .ForEach(serviceInstaller => serviceInstaller.Install(services)));

    /// <summary>
    /// Installs the modules using the <see cref="IModuleInstaller"/> implementations defined in the specified assemblies.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assemblies">The assembly to scan for module installer implementations.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection InstallModulesFromAssemblies(this IServiceCollection services, params Assembly[] assemblies) =>
        services.Tap(
            () => InstanceFactory
                .CreateFromAssemblies<IModuleInstaller>(assemblies)
                .ForEach(moduleInstaller => moduleInstaller.Install(services)));

    /// <summary>
    /// Adds all of the implementations of <see cref="ITransient"/> inside the specified assembly as transient.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for transient services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddTransientAsMatchingInterfaces(this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<ITransient>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsMatchingInterface()
                .WithTransientLifetime());

    /// <summary>
    /// Adds all of the implementations of <see cref="IScoped"/> inside the specified assembly as scoped.
    /// </summary>
    /// <param name="services">The services.</param>
    /// <param name="assembly">The assembly to scan for scoped services.</param>
    /// <returns>The same service collection so that multiple calls can be chained.</returns>
    public static IServiceCollection AddScopedAsMatchingInterfaces(this IServiceCollection services, Assembly assembly) =>
        services.Scan(scan =>
            scan.FromAssemblies(assembly)
                .AddClasses(filter => filter.AssignableTo<IScoped>(), false)
                .UsingRegistrationStrategy(RegistrationStrategy.Throw)
                .AsMatchingInterface()
                .WithTransientLifetime());
}
