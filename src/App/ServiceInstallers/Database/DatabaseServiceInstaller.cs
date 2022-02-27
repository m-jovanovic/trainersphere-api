using Infrastructure.Configuration;
using Infrastructure.Extensions;
using Shared.Extensions;

namespace App.ServiceInstallers.Database;

/// <summary>
/// Represents the database service installer.
/// </summary>
internal sealed class DatabaseServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .ConfigureOptions<ConnectionStringSetup>()
            .AddTransientAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .Tap(() => Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true);
}
