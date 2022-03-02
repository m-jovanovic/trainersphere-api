using System.Text.Json;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Persistence.Outbox;
using Shared.Extensions;

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

        IEnumerable<OutboxMessage> outboxMessages = CreateOutboxMessages(eventData.Context);

        eventData.Context.Set<OutboxMessage>().AddRange(outboxMessages);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static IEnumerable<OutboxMessage> CreateOutboxMessages(DbContext dbContext) =>
        dbContext
            .ChangeTracker
            .Entries<IEntity>()
            .Select(entityEntry => entityEntry.Entity)
            .SelectMany(entity =>
                entity.GetDomainEvents()
                    .Tap(entity.ClearDomainEvents))
            .Select(domainEvent => new OutboxMessage(
                domainEvent.Id,
                domainEvent.OccurredOnUtc,
                JsonSerializer.Serialize(domainEvent)))
            .ToList();
}
