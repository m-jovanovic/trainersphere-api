namespace Application.EventBus;

/// <summary>
/// Represents the abstract integration event.
/// </summary>
public abstract class IntegrationEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="IntegrationEvent"/> class.
    /// </summary>
    protected IntegrationEvent()
    {
        Id = Guid.NewGuid();
        CreatedOnUtc = DateTime.UtcNow;
    }

    /// <summary>
    /// Gets the integration event identifier.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the created on date and time.
    /// </summary>
    public DateTime CreatedOnUtc { get; private set; }
}
