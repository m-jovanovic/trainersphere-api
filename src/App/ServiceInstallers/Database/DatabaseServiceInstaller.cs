using Infrastructure.Configuration;

namespace App.ServiceInstallers.Database;

/// <summary>
/// Represents the database service installer.
/// </summary>
internal sealed class DatabaseServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) => services.ConfigureOptions<ConnectionStringSetup>();
}
