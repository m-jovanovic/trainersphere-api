using Dapper;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;

namespace TrainerSphere.Api.Host;

internal sealed class DatabaseHealthCheck(NpgsqlDataSource dataSource) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
            bool migrated = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT to_regclass('public.schema_migrations') IS NOT NULL",
                cancellationToken: cancellationToken));

            return migrated ? HealthCheckResult.Healthy() : HealthCheckResult.Unhealthy("Migrations have not been applied.");
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", ex);
        }
    }
}
