using System.Linq;
using Domain.Results;
using FluentAssertions;
using Modules.Users.Domain.UnitTests.Data.Users;
using Modules.Users.Domain.Users;
using Modules.Users.Domain.Users.Events;
using Xunit;

namespace Modules.Users.Domain.UnitTests.Users;

public class UserTests
{
    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldSucceed(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.IsSuccess.Should().BeTrue();
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldCreateCreateNewUser_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.Value.Should().BeEquivalentTo(data);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldRaiseUserCreatedDomainEvent_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.Value.GetDomainEvents().Single()
            .Should().BeOfType<UserCreatedDomainEvent>()
            .Which.Id.Should().Be(data.Id);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void ChangeBasicInformation_ShouldChangeBasicInformation(CreateUserRequest data)
    {
        // Arrange
        User user = User.Create(data.Id, data.IdentityProviderId, data.Email, string.Empty, string.Empty).Value;

        // Act
        user.ChangeBasicInformation(data.FirstName, data.LastName);

        // Assert
        user.FirstName.Should().Be(data.FirstName);
        user.LastName.Should().Be(data.LastName);
    }
}
