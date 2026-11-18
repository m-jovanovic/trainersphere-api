using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.SharedKernel.Outbox;

/// <summary>
/// Collects domain events raised by aggregates during a command so they are written to the module outbox in the
/// command's transaction.
/// </summary>
public interface IDomainEventCollector
{
    void Collect(string schema, AggregateRoot aggregate);

    void Collect(string schema, IDomainEvent domainEvent);

    Task FlushToOutboxAsync(IDbSession session, CancellationToken cancellationToken);

    void Clear();
}

internal static class SchemaName
{
    public static void EnsureValid(string schema)
    {
        if (string.IsNullOrEmpty(schema) || !schema.All(c => char.IsAsciiLetterLower(c) || c == '_'))
        {
            throw new ArgumentException($"Invalid schema name '{schema}'.", nameof(schema));
        }
    }
}

internal sealed class DomainEventCollector(MessageSignals signals) : IDomainEventCollector
{
    private readonly List<(string Schema, IDomainEvent Event)> _events = [];

    public void Collect(string schema, AggregateRoot aggregate)
    {
        ArgumentNullException.ThrowIfNull(aggregate);
        SchemaName.EnsureValid(schema);

        foreach (IDomainEvent domainEvent in aggregate.PopDomainEvents())
        {
            _events.Add((schema, domainEvent));
        }
    }

    public void Collect(string schema, IDomainEvent domainEvent)
    {
        ArgumentNullException.ThrowIfNull(domainEvent);
        SchemaName.EnsureValid(schema);

        _events.Add((schema, domainEvent));
    }

    public async Task FlushToOutboxAsync(IDbSession session, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(session);

        if (_events.Count == 0)
        {
            return;
        }

        (string Schema, IDomainEvent Event)[] pending = [.. _events];
        _events.Clear();

        foreach ((string schema, IDomainEvent domainEvent) in pending)
        {
            await session.ExecuteAsync(
                $"""
                INSERT INTO {schema}.outbox_messages (id, type, content, occurred_at)
                VALUES (@Id, @Type, @Content::jsonb, @OccurredAt)
                """,
                new
                {
                    domainEvent.Id,
                    Type = MessageTypeRegistry.NameOf(domainEvent.GetType()),
                    Content = MessageSerializer.Serialize(domainEvent),
                    domainEvent.OccurredAt,
                },
                cancellationToken);
        }

        foreach (string schema in pending.Select(p => p.Schema).Distinct())
        {
            session.OnCommitted(() => signals.Notify(schema, MessageKind.Outbox));
        }
    }

    public void Clear() => _events.Clear();
}
