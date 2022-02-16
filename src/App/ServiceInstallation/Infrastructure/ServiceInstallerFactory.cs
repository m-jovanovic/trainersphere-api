using System.Reflection;

namespace App.ServiceInstallation.Infrastructure;

/// <summary>
/// Represents the service installer factory.
/// </summary>
internal static class ServiceInstallerFactory
{
    /// <summary>
    /// Creates the service installer instances defined in the specified assembly.
    /// </summary>
    /// <param name="assembly">The assembly to scan for service installers.</param>
    /// <returns>The enumerable collection of service installers defined in the specified assembly.</returns>
    internal static IEnumerable<IServiceInstaller> CreateServiceInstallersFromAssembly(Assembly assembly) =>
        assembly.DefinedTypes
            .Where(IsAssignableToServiceInstaller)
            .Select(Activator.CreateInstance)
            .Cast<IServiceInstaller>();

    private static bool IsAssignableToServiceInstaller(TypeInfo typeInfo) =>
        typeof(IServiceInstaller).IsAssignableFrom(typeInfo) && !typeInfo.IsInterface && !typeInfo.IsAbstract;
}
