using Application.EventBus;
using Modules.Notifications.Application.Abstractions.Email;
using Modules.Users.IntegrationEvents;

namespace Modules.Notifications.Application.Users;

/// <summary>
/// Represents the <see cref="UserRegisteredIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly IEmailSender _emailSender;

    public UserRegisteredIntegrationEventHandler(IEmailSender emailSender) => _emailSender = emailSender;

    /// <inheritdoc />
    public async Task Handle(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await _emailSender.SendWelcomeAsync(
            new WelcomeEmailRequest(
                integrationEvent.Email,
                $"{integrationEvent.FirstName} {integrationEvent.LastName}"),
            cancellationToken);
}
