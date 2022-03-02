using Infrastructure.BackgroundJobs;

namespace Modules.Users.Infrastructure.BackgroundJobs.ProcessOutboxMessages;

/// <summary>
/// Represents the <see cref="ProcessOutboxMessagesJob"/> configuration.
/// </summary>
internal sealed class ProcessOutboxMessagesConfiguration : IRecurringJobConfiguration
{
    /// <inheritdoc />
    public string Name => typeof(ProcessOutboxMessagesJob).FullName!;

    /// <inheritdoc />
    public Type Type => typeof(ProcessOutboxMessagesJob);

    /// <inheritdoc />
    public int IntervalInSeconds => 5;
}
