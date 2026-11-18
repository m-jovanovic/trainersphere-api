using System.Net;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.Api.Host;
using TrainerSphere.IntegrationTests.Infrastructure;
using TrainerSphere.SharedKernel.Migrations;

namespace TrainerSphere.IntegrationTests.Host;

public sealed class MigrationTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Startup_OnEmptyDatabase_AppliesEveryModuleMigration()
    {
        IReadOnlyList<(string Module, int Version)> applied =
            await Factory.QueryAsync<(string Module, int Version)>("SELECT module, version FROM public.schema_migrations ORDER BY module, version");

        foreach (MigrationSource source in Factory.Services.GetServices<MigrationSource>())
        {
            int[] expected = [.. source.Load().Select(m => m.Version)];
            expected.ShouldNotBeEmpty();
            applied.Where(a => a.Module == source.Module).Select(a => a.Version).ShouldBe(expected);
        }
    }

    [Fact]
    public async Task Startup_OnEmptyDatabase_CreatesSchemaAndMessagingTablesPerModule()
    {
        IReadOnlyList<string> tables = await Factory.QueryAsync<string>(
            "SELECT table_schema || '.' || table_name FROM information_schema.tables WHERE table_type = 'BASE TABLE'");

        foreach (string schema in ModuleCatalog.All.Select(m => m.Schema))
        {
            tables.ShouldContain($"{schema}.outbox_messages");
            tables.ShouldContain($"{schema}.outbox_message_consumers");
            tables.ShouldContain($"{schema}.inbox_messages");
            tables.ShouldContain($"{schema}.inbox_message_consumers");
        }
    }

    [Fact]
    public async Task Run_WhenAlreadyMigrated_AppliesNothing()
    {
        MigrationRunner runner = Factory.Services.GetRequiredService<MigrationRunner>();

        IReadOnlyList<Migration> executed = await runner.RunAsync(Ct);

        executed.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_WhenAppliedMigrationWasChanged_Throws()
    {
        MigrationRunner runner = Factory.Services.GetRequiredService<MigrationRunner>();
        const string Select = "SELECT checksum FROM public.schema_migrations WHERE module = 'identity' AND version = 1";
        string original = await Factory.QuerySingleAsync<string>(Select);

        await Factory.ExecuteAsync("UPDATE public.schema_migrations SET checksum = 'tampered' WHERE module = 'identity' AND version = 1");

        try
        {
            InvalidOperationException error = await Should.ThrowAsync<InvalidOperationException>(() => runner.RunAsync(Ct));
            error.Message.ShouldContain("was changed after it was applied");
        }
        finally
        {
            await Factory.ExecuteAsync(
                "UPDATE public.schema_migrations SET checksum = @Original WHERE module = 'identity' AND version = 1",
                new { Original = original });
        }
    }

    [Fact]
    public async Task HealthReady_AfterMigrations_IsHealthy()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/health/ready", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task HealthLive_IsHealthy()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/health/live", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
    }
}
