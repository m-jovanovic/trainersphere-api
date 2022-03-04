using Infrastructure.EventBus;
using MassTransit;
using Modules.Users.Infrastructure.Idempotence;
using Modules.Users.IntegrationEvents;

namespace Modules.Users.Infrastructure.Consumers;

/// <summary>
/// Represents the users module consumer configuration.
/// </summary>
internal sealed class ConsumerConfiguration : IConsumerConfiguration
{
    /// <inheritdoc />
    public void AddConsumers(IRegistrationConfigurator registrationConfigurator) =>
        registrationConfigurator.AddConsumer<IdempotentConsumer<UserRegisteredIntegrationEvent>>();
}
