using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.InvitationEmail;

internal static class InvitationEmail
{
    public static EmailMessage Compose(InvitationIssued invitation, WebLinks links) =>
        new EmailBuilder($"{invitation.TrainerName} invited you to TrainerSphere")
            .Paragraph($"Hi {invitation.FirstName},")
            .Paragraph($"{invitation.TrainerName} wants to coach you on TrainerSphere: your workouts, sessions and progress in one place.")
            .Quote(invitation.Message)
            .Action("Accept invitation", links.Invitation(invitation.Token))
            .Footnote($"This link expires on {EmailBuilder.FormatUtc(invitation.ExpiresAt)}. If you weren't expecting it, you can ignore this email.")
            .Build(EmailKinds.Invitation, invitation.Email, invitation.FirstName, $"{invitation.TrainerName} invited you to TrainerSphere");
}

internal sealed class SendInvitationEmailHandler(EmailDispatcher emails, WebLinks links) : IIntegrationEventHandler<InvitationIssued>
{
    public Task Handle(InvitationIssued integrationEvent, CancellationToken cancellationToken) =>
        emails.SendAsync(InvitationEmail.Compose(integrationEvent, links), integrationEvent.Id, cancellationToken);
}
