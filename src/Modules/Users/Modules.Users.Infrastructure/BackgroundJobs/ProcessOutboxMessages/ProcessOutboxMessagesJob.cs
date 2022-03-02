using Quartz;

namespace Modules.Users.Infrastructure.BackgroundJobs.ProcessOutboxMessages;

/// <summary>
/// Represents the background job for processing outbox messages.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ProcessOutboxMessagesJob : IJob
{
    /// <inheritdoc />
    public Task Execute(IJobExecutionContext context)
    {
        Console.WriteLine(nameof(ProcessOutboxMessagesJob));

        return Task.CompletedTask;
    }
}
