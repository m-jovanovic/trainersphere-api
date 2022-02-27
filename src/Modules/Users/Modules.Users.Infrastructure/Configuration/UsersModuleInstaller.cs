using Application.Behaviors;
using Application.Time;
using FluentValidation;
using Infrastructure.Configuration;
using Infrastructure.Extensions;
using Infrastructure.Time;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Modules.Users.Persistence;
using Modules.Users.Persistence.Constants;
using Persistence.Data;
using Persistence.Extensions;
using Persistence.Interceptors;
using Shared.Extensions;

namespace Modules.Users.Infrastructure.Configuration;

/// <summary>
/// Represents the users module installer.
/// </summary>
public sealed class UsersModuleInstaller : IModuleInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .AddTransient<ISystemTime, SystemTime>()
            .AddMediatR(Application.AssemblyReference.Assembly)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true)
            .Tap(serviceCollection => serviceCollection.TryAddSingleton<UpdateAuditableEntitiesInterceptor>())
            .AddDbContextPool<UsersDbContext>((serviceProvider, options) =>
            {
                ConnectionString connectionString = serviceProvider.GetService<IOptions<ConnectionString>>()!.Value;

                options
                    .UseNpgsql(
                        connectionString,
                        dbContextOptionsBuilder => dbContextOptionsBuilder.WithMigrationHistoryInSchema(Schemas.Users))
                    .AddInterceptors(
                        serviceProvider.GetService<UpdateAuditableEntitiesInterceptor>()!);
            })
            .AddTransientAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddTransientAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .AddControllers()
            .AddApplicationPart(Endpoints.AssemblyReference.Assembly);
}
