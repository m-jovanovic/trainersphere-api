using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace TrainerSphere.SharedKernel.Migrations;

public sealed record Migration(string Module, int Version, string Name, string Sql)
{
    public string Checksum { get; } = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(Sql.ReplaceLineEndings("\n"))));
}

/// <summary>
/// Embedded <c>.sql</c> resources named <c>V0001__description.sql</c> under the resource prefix.
/// </summary>
public sealed partial record MigrationSource(string Module, Assembly Assembly, string ResourcePrefix)
{
    public IReadOnlyList<Migration> Load()
    {
        List<Migration> migrations = [];

        foreach (string resource in Assembly.GetManifestResourceNames().Where(r => r.StartsWith(ResourcePrefix + ".", StringComparison.Ordinal)))
        {
            string fileName = resource[(ResourcePrefix.Length + 1)..];
            Match match = FileNamePattern().Match(fileName);

            if (!match.Success)
            {
                throw new InvalidOperationException($"Migration resource '{resource}' does not match V0000__name.sql.");
            }

            using Stream stream = Assembly.GetManifestResourceStream(resource)!;
            using StreamReader reader = new(stream);

            migrations.Add(new Migration(
                Module,
                int.Parse(match.Groups["version"].Value, System.Globalization.CultureInfo.InvariantCulture),
                match.Groups["name"].Value,
                reader.ReadToEnd()));
        }

        IGrouping<int, Migration>? duplicate = migrations.GroupBy(m => m.Version).FirstOrDefault(g => g.Count() > 1);

        if (duplicate is not null)
        {
            throw new InvalidOperationException($"Module '{Module}' has more than one migration with version {duplicate.Key}.");
        }

        return [.. migrations.OrderBy(m => m.Version)];
    }

    [GeneratedRegex(@"^V(?<version>\d+)__(?<name>[A-Za-z0-9_]+)\.sql$")]
    private static partial Regex FileNamePattern();
}

/// <summary>
/// Applies pending migrations module by module, each in its own transaction, under a Postgres advisory lock so
/// concurrent instances never migrate at the same time.
/// </summary>
public sealed partial class MigrationRunner(NpgsqlDataSource dataSource, IEnumerable<MigrationSource> sources, ILogger<MigrationRunner> logger)
{
    private const long AdvisoryLockKey = 0x7472_6169_6e65_72; // "trainer"

    public async Task<IReadOnlyList<Migration>> RunAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);

        await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_lock(@Key)", new { Key = AdvisoryLockKey }, cancellationToken: cancellationToken));

        try
        {
            await connection.ExecuteAsync(new CommandDefinition(
                """
                CREATE TABLE IF NOT EXISTS public.schema_migrations (
                    module text NOT NULL,
                    version integer NOT NULL,
                    name text NOT NULL,
                    checksum text NOT NULL,
                    applied_at timestamptz NOT NULL DEFAULT now(),
                    PRIMARY KEY (module, version)
                )
                """,
                cancellationToken: cancellationToken));

            var applied = (await connection.QueryAsync<(string Module, int Version, string Checksum)>(
                new CommandDefinition("SELECT module, version, checksum FROM public.schema_migrations", cancellationToken: cancellationToken)))
                .ToDictionary(r => (r.Module, r.Version), r => r.Checksum);

            List<Migration> executed = [];

            foreach (Migration migration in sources.SelectMany(s => s.Load()))
            {
                if (applied.TryGetValue((migration.Module, migration.Version), out string? checksum))
                {
                    if (!string.Equals(checksum, migration.Checksum, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            $"Migration {migration.Module} V{migration.Version} ({migration.Name}) was changed after it was applied.");
                    }

                    continue;
                }

                await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);
                await connection.ExecuteAsync(new CommandDefinition(migration.Sql, transaction: transaction, cancellationToken: cancellationToken));
                await connection.ExecuteAsync(new CommandDefinition(
                    "INSERT INTO public.schema_migrations (module, version, name, checksum) VALUES (@Module, @Version, @Name, @Checksum)",
                    new { migration.Module, migration.Version, migration.Name, migration.Checksum },
                    transaction,
                    cancellationToken: cancellationToken));
                await transaction.CommitAsync(cancellationToken);

                LogApplied(logger, migration.Module, migration.Version, migration.Name);
                executed.Add(migration);
            }

            return executed;
        }
        finally
        {
            await connection.ExecuteAsync(new CommandDefinition("SELECT pg_advisory_unlock(@Key)", new { Key = AdvisoryLockKey }, cancellationToken: CancellationToken.None));
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Applied migration {Module} V{Version} {Name}")]
    private static partial void LogApplied(ILogger logger, string module, int version, string name);
}
