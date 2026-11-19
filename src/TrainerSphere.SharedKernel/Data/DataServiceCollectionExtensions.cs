using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace TrainerSphere.SharedKernel.Data;

public static class DataServiceCollectionExtensions
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, string connectionString)
    {
        DapperConfiguration.Configure();

        services.AddSingleton(_ => new NpgsqlDataSourceBuilder(connectionString).Build());
        services.AddScoped<IDbSession, DbSession>();
        return services;
    }
}
