using System.Threading;
using System.Threading.Tasks;
using Application.EventBus;
using Modules.Users.Application.UnitTests.Data.Users;
using Modules.Users.Application.Users.Commands.RegisterUser;
using Modules.Users.Domain.Users.Events;
using Modules.Users.IntegrationEvents;
using Moq;
using Xunit;

namespace Modules.Users.Application.UnitTests.Users.Commands.RegisterUser;

public sealed class TrainerUserRegisteredDomainEventHandlerTests
{
    [Theory]
    [ClassData(typeof(TrainerUserRegisteredDomainEventData))]
    public async Task Handle_ShouldPublishIntegrationEvent_WhenDomainEventIsHandled(TrainerUserRegisteredDomainEvent domainEvent)
    {
        // Arrange
        var eventBusMock = new Mock<IEventBus>();
        var handler = new TrainerUserRegisteredDomainEventHandler(eventBusMock.Object);

        // Act
        await handler.Handle(domainEvent, default);

        // Assert
        eventBusMock.Verify(
            x => x.PublishAsync(
                It.Is<TrainerUserRegisteredIntegrationEvent>(
                    integrationEvent =>
                        integrationEvent.Id == domainEvent.Id &&
                        integrationEvent.OccurredOnUtc == domainEvent.OccurredOnUtc &&
                        integrationEvent.UserId == domainEvent.UserId.Value &&
                        integrationEvent.Email == domainEvent.Email &&
                        integrationEvent.FirstName == domainEvent.FirstName &&
                        integrationEvent.LastName == domainEvent.LastName),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
