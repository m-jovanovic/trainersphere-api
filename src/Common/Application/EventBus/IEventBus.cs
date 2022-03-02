namespace Application.EventBus;

/// <summary>
/// Represents the event bus interface.
/// </summary>
public interface IEventBus
{
    /// <summary>
    /// Publishes the specified integration event to the event bus.
    /// </summary>
    /// <param name="integrationEvent">The integration event.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The result of the publish operation.</returns>
    Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default);
}
