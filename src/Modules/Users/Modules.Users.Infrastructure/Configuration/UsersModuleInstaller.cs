using Infrastructure.Configuration;
using Infrastructure.Extensions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Modules.Users.Persistence;
using Persistence.Data;

namespace Modules.Users.Infrastructure.Configuration;

/// <summary>
/// Represents the users module installer.
/// </summary>
public sealed class UsersModuleInstaller : IModuleInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .AddMediatR(Application.AssemblyReference.Assembly)
            .AddDbContextPool<UsersDbContext>((serviceProvider, options) =>
            {
                ConnectionString connectionString = serviceProvider.GetService<IOptions<ConnectionString>>()!.Value;

                options.UseNpgsql(connectionString);
            })
            .AddTransientAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(AssemblyReference.Assembly)
            .AddTransientAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .AddScopedAsMatchingInterfaces(Persistence.AssemblyReference.Assembly)
            .AddControllers()
            .AddApplicationPart(Endpoints.AssemblyReference.Assembly);
}
