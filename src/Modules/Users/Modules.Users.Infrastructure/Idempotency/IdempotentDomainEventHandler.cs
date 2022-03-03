using Application.Data;
using Application.Messaging;
using Domain.Primitives;

namespace Modules.Users.Infrastructure.Idempotency;

/// <summary>
/// Represents the idempotent domain event handler, which checks if the domain event has been handled previously.
/// </summary>
/// <typeparam name="TEvent">The domain event type.</typeparam>
internal sealed class IdempotentDomainEventHandler<TEvent> : IDomainEventHandler<TEvent>
    where TEvent : IDomainEvent
{
    private readonly IDomainEventHandler<TEvent> _decorated;
    private readonly ISqlQueryExecutor _sqlQueryExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotentDomainEventHandler{TEvent}"/> class.
    /// </summary>
    /// <param name="decorated">The decorated domain event handler.</param>
    /// <param name="sqlQueryExecutor">The SQL query executor.</param>
    public IdempotentDomainEventHandler(IDomainEventHandler<TEvent> decorated, ISqlQueryExecutor sqlQueryExecutor)
    {
        _decorated = decorated;
        _sqlQueryExecutor = sqlQueryExecutor;
    }

    /// <inheritdoc />
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        const string checkIfConsumedSql = @"
            SELECT EXISTS(
                SELECT 1
                FROM users.outbox_message_consumers
                WHERE id = @Id AND
                      name = @Name
            )";

        var parameters = new
        {
            notification.Id,
            _decorated.GetType().Name
        };

        bool isConsumed = await _sqlQueryExecutor.ExecuteScalarAsync<bool>(checkIfConsumedSql, parameters);

        if (isConsumed)
        {
            return;
        }

        await _decorated.Handle(notification, cancellationToken);

        const string insertConsumedSql = @"
            INSERT INTO users.outbox_message_consumers(id, name)
            VALUES (@Id, @Name)";

        await _sqlQueryExecutor.ExecuteAsync(insertConsumedSql, parameters);
    }
}
