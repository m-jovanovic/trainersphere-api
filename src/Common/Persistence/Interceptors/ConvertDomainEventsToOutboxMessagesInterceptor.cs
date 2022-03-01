using System.Text.Json;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Outbox;

namespace Persistence.Interceptors;

/// <summary>
/// Represents the interceptor for converting domain events to outbox messages.
/// </summary>
public sealed class ConvertDomainEventsToOutboxMessagesInterceptor : SaveChangesInterceptor
{
    /// <inheritdoc />
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is null)
        {
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        var entities = eventData.Context
            .ChangeTracker
            .Entries<IEntity>()
            .Select(entityEntry => entityEntry.Entity)
            .ToList();

        var outboxMessages = entities
            .SelectMany(entity => entity.GetDomainEvents())
            .Select(domainEvent => new OutboxMessage(
                domainEvent.Id,
                domainEvent.OccurredOnUtc,
                JsonSerializer.Serialize(domainEvent)))
            .ToList();

        entities.ForEach(entity => entity.ClearDomainEvents());

        eventData.Context.AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}
