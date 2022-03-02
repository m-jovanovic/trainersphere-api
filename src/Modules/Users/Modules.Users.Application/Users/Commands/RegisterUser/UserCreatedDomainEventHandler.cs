using Application.EventBus;
using Application.Messaging;
using Modules.Users.Domain.Users.Events;
using Modules.Users.IntegrationEvents;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="UserCreatedDomainEvent"/> handler.
/// </summary>
internal sealed class UserCreatedDomainEventHandler : IDomainEventHandler<UserCreatedDomainEvent>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserCreatedDomainEventHandler"/> class.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    public UserCreatedDomainEventHandler(IEventBus eventBus) => _eventBus = eventBus;

    /// <inheritdoc />
    public async Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken) =>
        await _eventBus.PublishAsync(
            new UserRegisteredIntegrationEvent(
                notification.Id,
                notification.OccurredOnUtc,
                notification.UserId.Value,
                notification.Email,
                notification.FirstName,
                notification.LastName),
            cancellationToken);
}
