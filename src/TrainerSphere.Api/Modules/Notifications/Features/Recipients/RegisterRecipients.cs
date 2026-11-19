using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.Recipients;

// Recipients are maintained by their own handlers, so a failed email never loses the recipient record that later
// emails (for example client-joined to the trainer) depend on.
internal sealed class RegisterTrainerRecipientHandler(RecipientRepository recipients, TimeProvider timeProvider) : IIntegrationEventHandler<TrainerRegistered>
{
    public Task Handle(TrainerRegistered integrationEvent, CancellationToken cancellationToken) =>
        recipients.UpsertAsync(RecipientMapping.Trainer(integrationEvent), timeProvider.UtcNow(), cancellationToken);
}

internal sealed class RegisterClientRecipientHandler(RecipientRepository recipients, TimeProvider timeProvider) : IIntegrationEventHandler<ClientRegistered>
{
    public Task Handle(ClientRegistered integrationEvent, CancellationToken cancellationToken) =>
        recipients.UpsertAsync(RecipientMapping.Client(integrationEvent), timeProvider.UtcNow(), cancellationToken);
}
