using Bogus;
using Xunit;

namespace Modules.Users.Domain.UnitTests.Data.Users;

internal sealed class CreateUserRequestData : TheoryData<CreateUserRequest>
{
    public CreateUserRequestData()
    {
        var faker = new Faker();

        Add(new CreateUserRequest(
            faker.Random.Guid(),
            faker.Internet.Ip(),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            faker.Internet.Email()));
    }
}
