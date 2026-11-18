using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Notifications.Features.Recipients;

internal sealed class UpdateRecipientNameHandler(RecipientRepository recipients, TimeProvider timeProvider) : IIntegrationEventHandler<UserProfileUpdated>
{
    public Task Handle(UserProfileUpdated integrationEvent, CancellationToken cancellationToken) =>
        recipients.UpdateNameAsync(integrationEvent.UserId, integrationEvent.FirstName, integrationEvent.LastName, timeProvider.UtcNow(), cancellationToken);
}
