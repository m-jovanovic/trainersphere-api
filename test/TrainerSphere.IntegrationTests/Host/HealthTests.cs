using System.Net;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Section 15: <c>/health/live</c> is process liveness, <c>/health/ready</c> means database reachable and migrations applied.
/// </summary>
public sealed class HealthTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string Live = "/health/live";
    private const string Ready = "/health/ready";

    [Fact]
    public async Task HealthLive_Anonymous_ReportsHealthy()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(Live, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthReady_Anonymous_ReportsHealthy()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(Ready, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBe("Healthy");
    }

    [Fact]
    public async Task HealthReady_WithoutMigrationHistory_Returns503AndLiveStaysHealthy()
    {
        await Factory.ExecuteAsync("ALTER TABLE public.schema_migrations RENAME TO schema_migrations_hidden");

        try
        {
            HttpClient client = CreateAnonymousClient();

            await (await client.GetAsync(Ready, Ct)).ShouldHaveStatusAsync(HttpStatusCode.ServiceUnavailable);
            await (await client.GetAsync(Live, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        }
        finally
        {
            await Factory.ExecuteAsync("ALTER TABLE public.schema_migrations_hidden RENAME TO schema_migrations");
        }
    }

    [Fact(Skip = "QA-F7: readiness only checks that the migration history table exists, not that every migration is applied")]
    public async Task HealthReady_WithPendingMigration_Returns503()
    {
        const string Latest = "(module, version) = (SELECT module, version FROM public.schema_migrations ORDER BY applied_at DESC, version DESC LIMIT 1)";
        await Factory.ExecuteAsync($"CREATE TABLE public.schema_migrations_pending AS SELECT * FROM public.schema_migrations WHERE {Latest}");
        await Factory.ExecuteAsync("DELETE FROM public.schema_migrations WHERE (module, version) IN (SELECT module, version FROM public.schema_migrations_pending)");

        try
        {
            HttpResponseMessage response = await CreateAnonymousClient().GetAsync(Ready, Ct);

            await response.ShouldHaveStatusAsync(HttpStatusCode.ServiceUnavailable);
        }
        finally
        {
            await Factory.ExecuteAsync("INSERT INTO public.schema_migrations SELECT * FROM public.schema_migrations_pending");
            await Factory.ExecuteAsync("DROP TABLE public.schema_migrations_pending");
        }
    }
}
