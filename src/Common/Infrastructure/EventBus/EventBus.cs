using Application.EventBus;
using Shared.ServiceLifetimes;

namespace Infrastructure.EventBus;

/// <summary>
/// Represents the event bus.
/// </summary>
public sealed class EventBus : IEventBus, ITransient
{
    /// <inheritdoc />
    public Task PublishAsync(IIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
