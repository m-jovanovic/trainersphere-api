using Application.Messaging;
using Modules.Users.Domain.Users.Events;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="UserCreatedDomainEvent"/> handler.
/// </summary>
internal sealed class UserCreatedDomainEventHandler : IDomainEventHandler<UserCreatedDomainEvent>
{
    /// <inheritdoc />
    public Task Handle(UserCreatedDomainEvent notification, CancellationToken cancellationToken)
    {
        Console.WriteLine(notification.UserId);

        return Task.CompletedTask;
    }
}
