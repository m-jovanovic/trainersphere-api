using Infrastructure.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modules.Users.Persistence;
using Modules.Users.Persistence.Constants;
using Persistence.Data;
using Persistence.Extensions;
using Persistence.Interceptors;
using Shared.Extensions;

namespace Modules.Users.Infrastructure.ServiceInstallers;

/// <summary>
/// Represents the user's module persistence service installer.
/// </summary>
internal sealed class PersistenceServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .Tap(services.TryAddSingleton<ConvertDomainEventsToOutboxMessagesInterceptor>)
            .Tap(services.TryAddSingleton<UpdateAuditableEntitiesInterceptor>)
            .AddDbContext<UsersDbContext>((serviceProvider, options) =>
            {
                ConnectionString connectionString = serviceProvider.GetService<IOptions<ConnectionString>>()!.Value;

                options
                    .UseNpgsql(
                        connectionString,
                        dbContextOptionsBuilder => dbContextOptionsBuilder.WithMigrationHistoryTableInSchema(Schemas.Users))
                    .UseSnakeCaseNamingConvention()
                    .AddInterceptors(
                        serviceProvider.GetService<ConvertDomainEventsToOutboxMessagesInterceptor>()!,
                        serviceProvider.GetService<UpdateAuditableEntitiesInterceptor>()!);
            });
}
