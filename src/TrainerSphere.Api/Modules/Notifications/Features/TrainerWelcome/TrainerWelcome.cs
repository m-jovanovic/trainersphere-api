using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.TrainerWelcome;

internal static class TrainerWelcomeEmail
{
    public static EmailMessage Compose(Recipient trainer, WebLinks links) =>
        new EmailBuilder($"Welcome to TrainerSphere, {trainer.FirstName}")
            .Paragraph("Your coaching workspace is ready.")
            .Paragraph("Start by inviting your first client, then build a workout template and assign it. Your exercise library already has the standard movements with illustrations.")
            .Action("Open TrainerSphere", links.Login())
            .Build(EmailKinds.TrainerWelcome, trainer.Email, trainer.FullName, "Welcome to TrainerSphere");
}

internal sealed class SendTrainerWelcomeHandler(EmailDispatcher emails, WebLinks links) : IIntegrationEventHandler<TrainerRegistered>
{
    public Task Handle(TrainerRegistered integrationEvent, CancellationToken cancellationToken) =>
        emails.SendAsync(TrainerWelcomeEmail.Compose(RecipientMapping.Trainer(integrationEvent), links), integrationEvent.Id, cancellationToken);
}
