namespace Application.EventBus
{
    /// <summary>
    /// Represents the event bus interface.
    /// </summary>
    public interface IEventBus
    {
        /// <summary>
        /// Publishes the specified integration event.
        /// </summary>
        /// <param name="integrationEvent">The integration event.</param>
        /// <returns>The result of the publish operation.</returns>
        Task PublishAsync(IntegrationEvent integrationEvent);
    }
}
