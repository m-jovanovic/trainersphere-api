using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Training.Features.Profiles;

internal sealed class TrainerRegisteredHandler(IProfileProjection projection) : IIntegrationEventHandler<TrainerRegistered>
{
    public Task Handle(TrainerRegistered integrationEvent, CancellationToken cancellationToken) =>
        projection.UpsertTrainerAsync(integrationEvent, cancellationToken);
}

internal sealed class ClientRegisteredHandler(IProfileProjection projection) : IIntegrationEventHandler<ClientRegistered>
{
    public Task Handle(ClientRegistered integrationEvent, CancellationToken cancellationToken) =>
        projection.UpsertClientAsync(integrationEvent, cancellationToken);
}

internal sealed class UserProfileUpdatedHandler(IProfileProjection projection) : IIntegrationEventHandler<UserProfileUpdated>
{
    public Task Handle(UserProfileUpdated integrationEvent, CancellationToken cancellationToken) =>
        projection.ApplyProfileUpdateAsync(integrationEvent, cancellationToken);
}
