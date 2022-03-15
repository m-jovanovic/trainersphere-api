using Application.EventBus;
using Application.Messaging;
using Modules.Users.Domain.Users.Events;
using Modules.Users.IntegrationEvents;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="ClientUserRegisteredDomainEvent"/> handler.
/// </summary>
internal sealed class ClientUserRegisteredDomainEventHandler : IDomainEventHandler<ClientUserRegisteredDomainEvent>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientUserRegisteredDomainEventHandler"/> class.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    public ClientUserRegisteredDomainEventHandler(IEventBus eventBus) => _eventBus = eventBus;

    /// <inheritdoc />
    public async Task Handle(ClientUserRegisteredDomainEvent notification, CancellationToken cancellationToken) =>
        await _eventBus.PublishAsync(
            new ClientUserRegisteredIntegrationEvent(
                notification.Id,
                notification.OccurredOnUtc,
                notification.UserId.Value,
                notification.Email,
                notification.FirstName,
                notification.LastName),
            cancellationToken);
}
