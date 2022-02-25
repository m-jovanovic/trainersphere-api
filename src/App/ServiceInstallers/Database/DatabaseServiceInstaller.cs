using Infrastructure.Configuration;
using Infrastructure.Extensions;

namespace App.ServiceInstallers.Database;

/// <summary>
/// Represents the database service installer.
/// </summary>
internal sealed class DatabaseServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .ConfigureOptions<ConnectionStringSetup>()
            .AddTransientAsMatchingInterfaces(Persistence.AssemblyReference.Assembly);
}
