using System.Reflection;
using Infrastructure.Configuration;
using Shared.Extensions;

namespace App.ServiceInstallation.Installers.Modules;

/// <summary>
/// Represents the modules service installer.
/// </summary>
internal sealed class ModuleServiceInstaller : IServiceInstaller
{
    private static readonly Assembly[] ModuleAssemblies =
    {
        Users.Infrastructure.AssemblyReference.Assembly
    };

    /// <inheritdoc />
    void IServiceInstaller.Install(IServiceCollection services) =>
        InstanceFactory
            .CreateFromAssemblies<IModuleInstaller>(ModuleAssemblies)
            .ForEach(moduleInstaller => moduleInstaller.Install(services));
}
