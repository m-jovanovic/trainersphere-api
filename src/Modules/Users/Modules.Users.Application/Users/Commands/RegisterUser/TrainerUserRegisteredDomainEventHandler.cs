using Application.EventBus;
using Application.Messaging;
using Modules.Users.Domain.Users.Events;
using Modules.Users.IntegrationEvents;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="TrainerUserRegisteredDomainEvent"/> handler.
/// </summary>
internal sealed class TrainerUserRegisteredDomainEventHandler : IDomainEventHandler<TrainerUserRegisteredDomainEvent>
{
    private readonly IEventBus _eventBus;

    /// <summary>
    /// Initializes a new instance of the <see cref="TrainerUserRegisteredDomainEventHandler"/> class.
    /// </summary>
    /// <param name="eventBus">The event bus.</param>
    public TrainerUserRegisteredDomainEventHandler(IEventBus eventBus) => _eventBus = eventBus;

    /// <inheritdoc />
    public async Task Handle(TrainerUserRegisteredDomainEvent notification, CancellationToken cancellationToken) =>
        await _eventBus.PublishAsync(
            new TrainerUserRegisteredIntegrationEvent(
                notification.Id,
                notification.OccurredOnUtc,
                notification.UserId.Value,
                notification.Email,
                notification.FirstName,
                notification.LastName),
            cancellationToken);
}
