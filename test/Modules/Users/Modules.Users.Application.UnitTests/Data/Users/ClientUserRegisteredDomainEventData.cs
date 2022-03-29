using Bogus;
using Domain.Time;
using Modules.Users.Domain.UserRegistrations;
using Modules.Users.Domain.Users;
using Modules.Users.Domain.Users.Events;
using Xunit;

namespace Modules.Users.Application.UnitTests.Data.Users;

internal sealed class ClientUserRegisteredDomainEventData : TheoryData<ClientUserRegisteredDomainEvent>
{
    public ClientUserRegisteredDomainEventData()
    {
        var faker = new Faker();

        Add(new ClientUserRegisteredDomainEvent(
            faker.Random.Guid(),
            SystemTimeProvider.UtcNow(),
            new UserRegistrationId(faker.Random.Guid()),
            new UserId(faker.Random.Guid()),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            faker.Internet.Email()));
    }
}
