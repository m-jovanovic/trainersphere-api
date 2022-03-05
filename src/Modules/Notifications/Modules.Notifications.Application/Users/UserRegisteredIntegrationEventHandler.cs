using Application.EventBus;
using Modules.Users.IntegrationEvents;

namespace Modules.Notifications.Application.Users;

/// <summary>
/// Represents the <see cref="UserRegisteredIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    /// <inheritdoc />
    public Task Handle(UserRegisteredIntegrationEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine($"User registered: {notification.UserId}");

        return Task.CompletedTask;
    }
}
