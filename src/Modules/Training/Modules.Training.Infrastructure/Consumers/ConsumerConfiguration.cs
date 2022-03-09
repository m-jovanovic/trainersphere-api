using Infrastructure.EventBus;
using MassTransit;

namespace Modules.Training.Infrastructure.Consumers;

/// <summary>
/// Represents the consumer configuration for the notifications module.
/// </summary>
internal sealed class ConsumerConfiguration : IConsumerConfiguration
{
    /// <inheritdoc />
    public void AddConsumers(IRegistrationConfigurator registrationConfigurator)
    {
        // Add consumers when necessary.
    }
}
