using TrainerSphere.SharedKernel.Migrations;

namespace TrainerSphere.Api.Host;

/// <summary>
/// Work that must finish before the application serves requests (migrations, reference data, demo seed).
/// </summary>
internal interface IStartupTask
{
    int Order { get; }

    Task ExecuteAsync(CancellationToken cancellationToken);
}

internal sealed class MigrationStartupTask(MigrationRunner runner, IConfiguration configuration) : IStartupTask
{
    public int Order => 0;

    public async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        if (configuration.GetValue("Database:MigrateOnStartup", defaultValue: true))
        {
            await runner.RunAsync(cancellationToken);
        }
    }
}

/// <summary>
/// Runs startup tasks in order. Registered before the message processors so their tables exist when they start.
/// </summary>
internal sealed class StartupTasksHostedService(IServiceScopeFactory scopeFactory) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        foreach (IStartupTask task in scope.ServiceProvider.GetServices<IStartupTask>().OrderBy(t => t.Order))
        {
            await task.ExecuteAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
