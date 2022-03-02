namespace Modules.Users.Infrastructure.BackgroundJobs.ProcessOutboxMessages;

/// <summary>
/// Represents the <see cref="ProcessOutboxMessagesJob"/> options.
/// </summary>
internal sealed class ProcessOutboxMessagesOptions
{
    /// <summary>
    /// Gets the interval in seconds.
    /// </summary>
    public int IntervalInSeconds { get; init; }
}
