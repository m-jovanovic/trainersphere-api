using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.SharedKernel.Outbox;

internal sealed class OutboxEventBus(IDbSession session, MessageHandlerRegistry registry, MessageSignals signals) : IEventBus
{
    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken)
        where TEvent : IIntegrationEvent
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        Type eventType = integrationEvent.GetType();

        foreach (string schema in registry.GetSubscriberSchemas(eventType))
        {
            await session.ExecuteAsync(
                $"""
                INSERT INTO {schema}.inbox_messages (id, type, content, occurred_at)
                VALUES (@Id, @Type, @Content::jsonb, @OccurredAt)
                ON CONFLICT (id) DO NOTHING
                """,
                new
                {
                    integrationEvent.Id,
                    Type = MessageTypeRegistry.NameOf(eventType),
                    Content = MessageSerializer.Serialize(integrationEvent),
                    integrationEvent.OccurredAt,
                },
                cancellationToken);

            session.OnCommitted(() => signals.Notify(schema, MessageKind.Inbox));
        }
    }
}
