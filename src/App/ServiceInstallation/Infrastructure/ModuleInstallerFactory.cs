using System.Reflection;
using Infrastructure.Configuration;

namespace App.ServiceInstallation.Infrastructure;

/// <summary>
/// Represents the module installer factory.
/// </summary>
internal static class ModuleInstallerFactory
{
    /// <summary>
    /// Creates the modules installer instances defined in the specified assemblies.
    /// </summary>
    /// <param name="assemblies">The assemblies to scan for service installers.</param>
    /// <returns>The enumerable collection of module installers defined in the specified assemblies.</returns>
    internal static IEnumerable<IModuleInstaller> CreateModuleInstallerFromAssemblies(Assembly[] assemblies) =>
        assemblies.SelectMany(assembly => assembly.DefinedTypes)
            .Where(IsAssignableToModuleInstaller)
            .Select(Activator.CreateInstance)
            .Cast<IModuleInstaller>();

    private static bool IsAssignableToModuleInstaller(TypeInfo typeInfo) =>
        typeof(IModuleInstaller).IsAssignableFrom(typeInfo) && !typeInfo.IsInterface && !typeInfo.IsAbstract;
}
