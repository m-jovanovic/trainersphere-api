using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.PasswordResetEmail;

internal static class PasswordResetEmail
{
    public static EmailMessage Compose(PasswordResetRequested request, WebLinks links) =>
        new EmailBuilder("Reset your password")
            .Paragraph($"Hi {request.FirstName},")
            .Paragraph("Someone asked to reset the password of your TrainerSphere account. Use the button below to choose a new one.")
            .Action("Choose a new password", links.PasswordReset(request.Token))
            .Footnote($"This link can be used once and expires on {EmailBuilder.FormatUtc(request.ExpiresAt)}. If you didn't ask for it, ignore this email; your password stays the same.")
            .Build(EmailKinds.PasswordReset, request.Email, request.FirstName, "Reset your TrainerSphere password");
}

internal sealed class SendPasswordResetEmailHandler(EmailDispatcher emails, WebLinks links) : IIntegrationEventHandler<PasswordResetRequested>
{
    public Task Handle(PasswordResetRequested integrationEvent, CancellationToken cancellationToken) =>
        emails.SendAsync(PasswordResetEmail.Compose(integrationEvent, links), integrationEvent.Id, cancellationToken);
}
