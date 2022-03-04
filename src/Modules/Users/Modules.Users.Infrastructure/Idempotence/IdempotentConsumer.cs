using Application.EventBus;
using MassTransit;

namespace Modules.Users.Infrastructure.Idempotence;

/// <summary>
/// Represents the idempotent event bus message consumer.
/// </summary>
/// <typeparam name="TIntegrationEvent">The integration event type.</typeparam>
internal sealed class IdempotentConsumer<TIntegrationEvent> : IConsumer<TIntegrationEvent>
    where TIntegrationEvent : class, IIntegrationEvent
{
    /// <inheritdoc />
    public Task Consume(ConsumeContext<TIntegrationEvent> context)
    {
        Console.WriteLine(context.Message?.ToString());

        return Task.CompletedTask;
    }
}
