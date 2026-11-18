using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Data;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.ClientJoined;

internal static class ClientJoinedEmail
{
    public static EmailMessage Compose(Recipient trainer, Recipient client, WebLinks links) =>
        new EmailBuilder($"{client.FullName} joined TrainerSphere")
            .Paragraph($"Hi {trainer.FirstName},")
            .Paragraph($"{client.FullName} ({client.Email}) accepted your invitation and is now one of your clients. Assign a program to get them started.")
            .Action("View client", links.Client(client.UserId))
            .Build(EmailKinds.ClientJoined, trainer.Email, trainer.FullName, $"{client.FullName} accepted your invitation");
}

internal sealed class NotifyTrainerOfNewClientHandler(RecipientRepository recipients, EmailDispatcher emails, WebLinks links, TimeProvider timeProvider)
    : IIntegrationEventHandler<ClientRegistered>
{
    public async Task Handle(ClientRegistered integrationEvent, CancellationToken cancellationToken)
    {
        Recipient client = new(
            integrationEvent.UserId,
            integrationEvent.Email,
            integrationEvent.FirstName,
            integrationEvent.LastName,
            Recipient.ClientRole,
            integrationEvent.TrainerId);

        await recipients.UpsertAsync(client, timeProvider.UtcNow(), cancellationToken);

        // The inbox is processed in event order, so the trainer's TrainerRegistered has been handled already unless it
        // failed; throwing retries this message after that one succeeds.
        Recipient trainer = await recipients.GetAsync(integrationEvent.TrainerId, cancellationToken)
            ?? throw new InvalidOperationException($"Trainer {integrationEvent.TrainerId} is not a known recipient yet.");

        await emails.SendAsync(ClientJoinedEmail.Compose(trainer, client, links), integrationEvent.Id, cancellationToken);
    }
}
