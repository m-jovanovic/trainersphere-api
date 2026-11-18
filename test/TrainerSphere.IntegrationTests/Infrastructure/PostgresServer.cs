using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(TrainerSphere.IntegrationTests.Infrastructure.PostgresServer))]

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// One Postgres container for the whole test run. Every <see cref="ApiFactory"/> gets its own empty database on it,
/// so each test class starts from a freshly migrated schema and classes can run in parallel.
/// </summary>
public sealed class PostgresServer : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18-alpine")
        .WithCommand("-c", "fsync=off", "-c", "synchronous_commit=off", "-c", "full_page_writes=off", "-c", "max_connections=500")
        .Build();

    public async ValueTask InitializeAsync() => await _container.StartAsync();

    public async ValueTask DisposeAsync() => await _container.DisposeAsync();

    [System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "CA2100:Review SQL queries for security vulnerabilities", Justification = "The database name is generated here, never user input.")]
    public async Task<string> CreateDatabaseAsync(CancellationToken cancellationToken)
    {
        string name = $"ts_test_{Guid.CreateVersion7():N}";

        await using (var admin = new NpgsqlConnection(_container.GetConnectionString()))
        {
            await admin.OpenAsync(cancellationToken);
            await using NpgsqlCommand command = new($"CREATE DATABASE \"{name}\"", admin);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        return new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = name }.ConnectionString;
    }
}
