using System.Collections.Concurrent;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Integration events published while a fixture's host runs. Tests use it to read one-time tokens that only ever
/// leave the API in emails (invitation and password reset links) and to assert which events a request raised.
/// </summary>
internal sealed class PublishedEvents
{
    private readonly ConcurrentQueue<IIntegrationEvent> _events = new();

    public IReadOnlyList<IIntegrationEvent> All => [.. _events];

    public IReadOnlyList<TEvent> OfType<TEvent>()
        where TEvent : IIntegrationEvent => [.. _events.OfType<TEvent>()];

    public TEvent Last<TEvent>(Func<TEvent, bool> predicate)
        where TEvent : IIntegrationEvent =>
        _events.OfType<TEvent>().LastOrDefault(predicate)
        ?? throw new InvalidOperationException($"No {typeof(TEvent).Name} matching the predicate was published.");

    public void Add(IIntegrationEvent integrationEvent) => _events.Enqueue(integrationEvent);
}

internal sealed class RecordingEventBus(IEventBus inner, PublishedEvents events) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        await inner.PublishAsync(integrationEvent, cancellationToken);
        events.Add(integrationEvent);
    }
}
