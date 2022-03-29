using Bogus;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;
using Xunit;

namespace Modules.Users.Application.UnitTests.Data.Users;

internal sealed class RegisterUserCommandData : TheoryData<RegisterUserCommand>
{
    public RegisterUserCommandData()
    {
        var faker = new Faker();

        Add(new RegisterUserCommand(
            faker.Internet.Ip(),
            faker.Name.FirstName(),
            faker.Name.LastName(),
            faker.Internet.Email()));
    }
}
