using MediatR;

namespace Application.EventBus;

/// <summary>
/// Represents the integration event handler interface.
/// </summary>
/// <typeparam name="TEvent">The event type.</typeparam>
public interface IIntegrationEventHandler<in TEvent> : INotificationHandler<TEvent>
    where TEvent : IIntegrationEvent
{
}
