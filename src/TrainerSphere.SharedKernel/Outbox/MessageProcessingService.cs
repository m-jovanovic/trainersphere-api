using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace TrainerSphere.SharedKernel.Outbox;

public static class MessageProcessingServiceCollectionExtensions
{
    public static IServiceCollection AddMessageProcessor(this IServiceCollection services, string schema, MessageKind kind)
    {
        services.AddSingleton<IMessageProcessor>(sp => ActivatorUtilities.CreateInstance<MessageProcessor>(sp, schema, kind));
        return services;
    }

    public static IServiceCollection AddMessageProcessingService(this IServiceCollection services)
    {
        services.AddHostedService<MessageProcessingService>();
        return services;
    }
}

internal sealed partial class MessageProcessingService(
    IEnumerable<IMessageProcessor> processors,
    MessageSignals signals,
    IOptions<MessagingOptions> options,
    ILogger<MessageProcessingService> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled)
        {
            return Task.CompletedTask;
        }

        return Task.WhenAll(processors.Select(p => RunAsync(p, stoppingToken)));
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Processing {Schema} {Kind} messages failed")]
    private static partial void LogProcessingFailed(ILogger logger, Exception exception, string schema, MessageKind kind);

    private async Task RunAsync(IMessageProcessor processor, CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromMilliseconds(options.Value.PollingIntervalMilliseconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed = await processor.ProcessBatchAsync(stoppingToken);

                if (processed > 0)
                {
                    continue;
                }

                await signals.WaitAsync(processor.Schema, processor.Kind, interval, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (ex is NpgsqlException or InvalidOperationException or TimeoutException)
            {
                LogProcessingFailed(logger, ex, processor.Schema, processor.Kind);
                await Task.Delay(interval, stoppingToken);
            }
        }
    }
}
