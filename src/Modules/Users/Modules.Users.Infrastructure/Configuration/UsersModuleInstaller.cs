using Infrastructure.Configuration;
using Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Infrastructure.Configuration;

/// <summary>
/// Represents the users module installer.
/// </summary>
public sealed class UsersModuleInstaller : IModuleInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .InstallServicesFromAssembly(AssemblyReference.Assembly)
            .AddTransientAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddTransientAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(Persistence.AssemblyReference.Assembly);
}
