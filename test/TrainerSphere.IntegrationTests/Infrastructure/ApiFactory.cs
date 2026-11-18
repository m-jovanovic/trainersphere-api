using System.Net.Http.Headers;
using Dapper;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// The real API host (production <c>Program</c>, middleware and modules) on its own empty database. Startup runs the
/// migrations, so every fixture also proves the migrations apply to an empty database.
/// </summary>
public class ApiFactory(PostgresServer postgres) : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string SigningKey = "integration-tests-signing-key-0123456789abcdef";

    public string ConnectionString { get; private set; } = string.Empty;

    public NpgsqlDataSource DataSource => Services.GetRequiredService<NpgsqlDataSource>();

    /// <summary>
    /// Gets every integration event published through the event bus, recorded at publish time (also for requests
    /// whose transaction later rolls back).
    /// </summary>
    internal PublishedEvents Events { get; } = new();

    /// <summary>
    /// Gets every email the API sent (SMTP delivery is replaced unless <see cref="CaptureEmails"/> is false).
    /// </summary>
    internal CapturedEmails Emails { get; } = new();

    /// <summary>
    /// Gets a value indicating whether emails go to <see cref="Emails"/> instead of SMTP.
    /// </summary>
    protected virtual bool CaptureEmails => true;

    public virtual async ValueTask InitializeAsync()
    {
        ConnectionString = await postgres.CreateDatabaseAsync(TestContext.Current.CancellationToken);

        // Starting the server runs the startup tasks (migrations, reference data) before any test sends a request.
        _ = Server;
    }

    /// <summary>
    /// An anonymous client. It keeps cookies, and its base address is https so the Secure refresh cookie round-trips.
    /// </summary>
    public HttpClient CreateAnonymousClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true,
        AllowAutoRedirect = false,
    });

    public HttpClient CreateBearerClient(string accessToken)
    {
        HttpClient client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? parameters = null)
    {
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        return [.. await connection.QueryAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken))];
    }

    public async Task<T> QuerySingleAsync<T>(string sql, object? parameters = null)
    {
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        return await connection.QuerySingleAsync<T>(new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));
    }

    public async Task<int> ExecuteAsync(string sql, object? parameters = null)
    {
        await using NpgsqlConnection connection = await DataSource.OpenConnectionAsync(TestContext.Current.CancellationToken);
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Waits until every module's outbox and inbox is drained, so asynchronous effects (Training profiles, emails)
    /// are visible. Fails when a message cannot be processed, quoting the handler error.
    /// </summary>
    public async Task WaitForMessagesAsync(TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));
        string pending = string.Join(
            " UNION ALL ",
            ModuleCatalog.All.SelectMany(m => new[] { $"{m.Schema}.outbox_messages", $"{m.Schema}.inbox_messages" })
                .Select(table => $"SELECT '{table}' AS source, type, attempts, error FROM {table} WHERE processed_at IS NULL"));

        while (true)
        {
            IReadOnlyList<PendingMessage> messages = await QueryAsync<PendingMessage>(pending);

            if (messages.Count == 0)
            {
                return;
            }

            PendingMessage? failed = messages.FirstOrDefault(m => m.Error is not null);

            if (failed is not null || DateTime.UtcNow > deadline)
            {
                throw new InvalidOperationException(
                    "Messages were not processed:" + Environment.NewLine +
                    string.Join(Environment.NewLine, messages.Select(m => $"{m.Source} {m.Type} attempts={m.Attempts}: {m.Error ?? "pending"}")));
            }

            await Task.Delay(25, TestContext.Current.CancellationToken);
        }
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Override to change settings for a dedicated fixture (for example a low rate limit). Keys use the
    /// configuration path syntax (<c>Section:Key</c>).
    /// </summary>
    protected virtual void ConfigureSettings(IDictionary<string, string?> settings)
    {
    }

    protected virtual void ConfigureServices(IServiceCollection services)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        Dictionary<string, string?> settings = new(StringComparer.Ordinal)
        {
            ["ConnectionStrings:Database"] = ConnectionString,
            ["Database:MigrateOnStartup"] = "true",
            ["Database:SeedDemoData"] = "false",
            ["Auth:SigningKey"] = SigningKey,
            ["Auth:RefreshCookieSecure"] = "true",
            ["RateLimiting:AuthPermitLimitPerMinute"] = "100000",
            ["Messaging:PollingIntervalMilliseconds"] = "50",
            ["Messaging:MaxAttempts"] = "3",
            ["Messaging:RetryBaseDelayMilliseconds"] = "20",
            ["OpenApi:Enabled"] = "true",
            ["App:WebBaseUrl"] = "http://web.test",
            ["Logging:LogLevel:Default"] = "Warning",
        };

        ConfigureSettings(settings);

        builder.UseEnvironment("Testing");

        foreach ((string key, string? value) in settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            ServiceDescriptor eventBus = services.Single(d => d.ServiceType == typeof(IEventBus));
            Type implementation = eventBus.ImplementationType
                ?? throw new InvalidOperationException("IEventBus is expected to be registered by implementation type.");

            services.Remove(eventBus);
            services.AddScoped<IEventBus>(sp => new RecordingEventBus((IEventBus)ActivatorUtilities.CreateInstance(sp, implementation), Events));

            if (CaptureEmails)
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(Emails);
            }

            ConfigureServices(services);
        });
    }

    private sealed record PendingMessage(string Source, string Type, int Attempts, string? Error);
}
