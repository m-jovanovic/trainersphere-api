using Microsoft.Extensions.Diagnostics.HealthChecks;
using Npgsql;
using TrainerSphere.SharedKernel.Migrations;

namespace TrainerSphere.Api.Host;

internal sealed class DatabaseHealthCheck(MigrationRunner migrations) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            IReadOnlyList<Migration> pending = await migrations.GetPendingAsync(cancellationToken);

            return pending.Count == 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy($"{pending.Count} migration(s) are not applied.");
        }
        catch (NpgsqlException ex)
        {
            return HealthCheckResult.Unhealthy("The database is unreachable.", ex);
        }
    }
}
