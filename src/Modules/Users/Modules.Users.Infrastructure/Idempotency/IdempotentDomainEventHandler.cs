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

    /// <summary>
    /// Initializes a new instance of the <see cref="IdempotentDomainEventHandler{TEvent}"/> class.
    /// </summary>
    /// <param name="decorated">The decorated domain event handler.</param>
    public IdempotentDomainEventHandler(IDomainEventHandler<TEvent> decorated) => _decorated = decorated;

    /// <inheritdoc />
    public async Task Handle(TEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine("Checking idempotency...");

        await _decorated.Handle(notification, cancellationToken);

        Console.WriteLine("Saving idempotency...");
    }
}
