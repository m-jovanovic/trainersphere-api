using Bogus;
using Domain.Time;
using Modules.Users.Domain.Users;
using Modules.Users.Domain.Users.Events;
using Xunit;

namespace Modules.Users.Application.UnitTests.Data.Users;

internal sealed class UserRegisteredDomainEventData : TheoryData<UserRegisteredDomainEvent>
{
    public UserRegisteredDomainEventData()
    {
        var faker = new Faker();

        Add(new UserRegisteredDomainEvent(
            faker.Random.Guid(),
            SystemTimeProvider.UtcNow(),
            new UserId(faker.Random.Guid()),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            faker.Internet.Email()));
    }
}
