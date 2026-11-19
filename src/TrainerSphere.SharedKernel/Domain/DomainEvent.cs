namespace TrainerSphere.SharedKernel.Domain;

/// <summary>
/// Something that happened inside a module, persisted to the module outbox in the same transaction as the change.
/// </summary>
public interface IDomainEvent
{
    Guid Id { get; }

    DateTime OccurredAt { get; }
}

public abstract record DomainEvent(Guid Id, DateTime OccurredAt) : IDomainEvent;

public abstract class AggregateRoot
{
    private readonly List<IDomainEvent> _domainEvents = [];

    public IReadOnlyList<IDomainEvent> DomainEvents => _domainEvents;

    public IReadOnlyList<IDomainEvent> PopDomainEvents()
    {
        IDomainEvent[] events = [.. _domainEvents];
        _domainEvents.Clear();
        return events;
    }

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}
