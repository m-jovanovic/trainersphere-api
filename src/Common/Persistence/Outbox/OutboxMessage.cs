namespace Persistence.Outbox;

/// <summary>
/// Represents the outbox message.
/// </summary>
public sealed class OutboxMessage
{
    /// <summary>
    /// Initializes a new instance of the <see cref="OutboxMessage"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="occurredOn">The occurred on date.</param>
    /// <param name="content">The content.</param>
    public OutboxMessage(Guid id, DateOnly occurredOn, string content)
    {
        Id = id;
        OccurredOn = occurredOn;
        Content = content;
    }

    /// <summary>
    /// Gets the identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the occurred on date.
    /// </summary>
    public DateOnly OccurredOn { get; private set; }

    /// <summary>
    /// Gets the content.
    /// </summary>
    public string Content { get; private set; }

    /// <summary>
    /// Gets the processed on date and time in UTC format.
    /// </summary>
    public DateTime? ProcessedOnUtc { get; private set; }
}
