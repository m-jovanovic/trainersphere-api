using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.SharedKernel.Messaging;

public interface IDomainEventHandler<in TEvent>
    where TEvent : IDomainEvent
{
    Task Handle(TEvent domainEvent, CancellationToken cancellationToken);
}

/// <summary>
/// A message published by one module and consumed by others through their inboxes.
/// </summary>
public interface IIntegrationEvent
{
    Guid Id { get; }

    DateTime OccurredAt { get; }
}

public interface IIntegrationEventHandler<in TEvent>
    where TEvent : IIntegrationEvent
{
    Task Handle(TEvent integrationEvent, CancellationToken cancellationToken);
}

/// <summary>
/// Publishes integration events to the inbox of every subscribed module, inside the current database transaction.
/// </summary>
public interface IEventBus
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent;
}

public abstract record IntegrationEvent(Guid Id, DateTime OccurredAt) : IIntegrationEvent;
