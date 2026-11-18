using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Training.Features.Invitations;

internal sealed class InvitationIssuedProjectionHandler(IInvitationProjection invitations) : IIntegrationEventHandler<InvitationIssued>
{
    public Task Handle(InvitationIssued integrationEvent, CancellationToken cancellationToken) =>
        invitations.TrackAsync(integrationEvent, cancellationToken);
}

internal sealed class InvitationAcceptedProjectionHandler(IInvitationProjection invitations) : IIntegrationEventHandler<ClientRegistered>
{
    public Task Handle(ClientRegistered integrationEvent, CancellationToken cancellationToken) =>
        invitations.RemoveAsync(integrationEvent.InvitationId, cancellationToken);
}

internal sealed class InvitationCancelledProjectionHandler(IInvitationProjection invitations) : IIntegrationEventHandler<InvitationCancelled>
{
    public Task Handle(InvitationCancelled integrationEvent, CancellationToken cancellationToken) =>
        invitations.RemoveAsync(integrationEvent.InvitationId, cancellationToken);
}
