using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Outbox;

namespace TrainerSphere.Api.Host;

/// <summary>
/// A module's part of the demo data. Runs inside a transaction; returns false when its data already exists.
/// Seeders go through the module's domain and event bus, so other modules receive the same integration events as
/// for real sign-ups.
/// </summary>
internal interface IDemoSeeder
{
    int Order { get; }

    Task<bool> SeedAsync(CancellationToken cancellationToken);
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S2068:Credentials should not be hard-coded", Justification = "Documented password of the public demo accounts; seeding is off outside Development and docker compose.")]
internal static class DemoAccounts
{
    public const string Password = "Demo123!";
    public const string TimeZone = "Europe/Belgrade";
    public const string TrainerEmail = "coach@trainersphere.dev";
    public const string PendingInviteEmail = "riley@trainersphere.dev";

    public static IReadOnlyList<(string Email, string FirstName, string LastName)> Clients { get; } =
    [
        ("alex@trainersphere.dev", "Alex", "Rivera"),
        ("sam@trainersphere.dev", "Sam", "Chen"),
        ("jordan@trainersphere.dev", "Jordan", "Patel"),
    ];
}

/// <summary>
/// Seeds demo data when <c>Database:SeedDemoData</c> is on. After each seeder it drains every inbox and outbox, so
/// later seeders can rely on the projections earlier ones caused (for example Training client profiles).
/// </summary>
internal sealed partial class DemoSeedStartupTask(
    IServiceScopeFactory scopeFactory,
    IEnumerable<IMessageProcessor> processors,
    IConfiguration configuration,
    ILogger<DemoSeedStartupTask> logger) : IStartupTask
{
    private const int MaxDrainPasses = 200;

    public int Order => 100;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (!configuration.GetValue("Database:SeedDemoData", defaultValue: false))
        {
            return;
        }

        int[] orders;

        await using (AsyncServiceScope scope = scopeFactory.CreateAsyncScope())
        {
            orders = [.. scope.ServiceProvider.GetServices<IDemoSeeder>().Select(s => s.Order).Order()];
        }

        foreach (int order in orders)
        {
            await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();
            IDemoSeeder seeder = scope.ServiceProvider.GetServices<IDemoSeeder>().Single(s => s.Order == order);
            IDbSession session = scope.ServiceProvider.GetRequiredService<IDbSession>();

            await session.BeginTransactionAsync(cancellationToken);
            bool seeded = await seeder.SeedAsync(cancellationToken);
            await session.CommitAsync(cancellationToken);

            LogSeeder(logger, seeder.GetType().Name, seeded ? "seeded" : "already present");
            await DrainMessagesAsync(cancellationToken);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Demo data {Seeder}: {Outcome}")]
    private static partial void LogSeeder(ILogger logger, string seeder, string outcome);

    private async Task DrainMessagesAsync(CancellationToken cancellationToken)
    {
        for (int pass = 0; pass < MaxDrainPasses; pass++)
        {
            int processed = 0;

            foreach (IMessageProcessor processor in processors)
            {
                processed += await processor.ProcessBatchAsync(cancellationToken);
            }

            if (processed == 0)
            {
                return;
            }
        }
    }
}
