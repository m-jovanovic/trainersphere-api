using Microsoft.EntityFrameworkCore;
using Modules.Users.Persistence;

namespace App.StartupTasks;

/// <summary>
/// Represents the startup task for migrating the database in the development environment only.
/// </summary>
internal sealed class MigrateDatabaseStartupTask : BackgroundService
{
    private readonly IHostEnvironment _environment;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateDatabaseStartupTask"/> class.
    /// </summary>
    /// <param name="environment">The environment.</param>
    /// <param name="serviceProvider">The service provider.</param>
    public MigrateDatabaseStartupTask(IHostEnvironment environment, IServiceProvider serviceProvider)
    {
        _environment = environment;
        _serviceProvider = serviceProvider;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_environment.IsDevelopment())
        {
            return;
        }

        using IServiceScope scope = _serviceProvider.CreateScope();

        UsersDbContext dbContext = scope.ServiceProvider.GetRequiredService<UsersDbContext>();

        await dbContext.Database.MigrateAsync(stoppingToken);
    }
}
