using Bogus;
using Domain.Time;
using Modules.Users.Domain.Users;
using Modules.Users.Domain.Users.Events;
using Xunit;

namespace Modules.Users.Application.UnitTests.Data.Users;

internal sealed class TrainerUserRegisteredDomainEventData : TheoryData<TrainerUserRegisteredDomainEvent>
{
    public TrainerUserRegisteredDomainEventData()
    {
        var faker = new Faker();

        Add(new TrainerUserRegisteredDomainEvent(
            faker.Random.Guid(),
            SystemTimeProvider.UtcNow(),
            new UserId(faker.Random.Guid()),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            faker.Internet.Email()));
    }
}
