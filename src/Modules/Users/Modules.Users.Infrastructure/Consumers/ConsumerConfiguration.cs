using Infrastructure.EventBus;
using MassTransit;
using Modules.Users.Infrastructure.Authorization.Permissions;

namespace Modules.Users.Infrastructure.Consumers;

/// <summary>
/// Represents the consumer configuration for the notifications module.
/// </summary>
internal sealed class ConsumerConfiguration : IConsumerConfiguration
{
    /// <inheritdoc />
    public void AddConsumers(IRegistrationConfigurator registrationConfigurator) =>
        registrationConfigurator.AddConsumer<UserPermissionsRequestConsumer>();
}
