using System.Linq;
using FluentAssertions;
using FluentAssertions.Primitives;
using Modules.Users.Domain.Roles;
using Modules.Users.Domain.UnitTests.Data.Users;
using Modules.Users.Domain.Users;
using Modules.Users.Domain.Users.Events;
using Shared.Results;
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
    public void Create_ShouldCreateNewUser_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.Value.Should().BeEquivalentTo(data);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldRaiseUserRegisteredDomainEvent_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        AndWhichConstraint<ObjectAssertions, UserRegisteredDomainEvent> domainEvent = user.Value
            .GetDomainEvents()
            .Single()
            .Should()
            .BeOfType<UserRegisteredDomainEvent>();

        domainEvent.Which.UserId.Should().Be(data.Id);
        domainEvent.Which.Email.Should().Be(data.Email);
        domainEvent.Which.FirstName.Should().Be(data.FirstName);
        domainEvent.Which.LastName.Should().Be(data.LastName);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldAddRegisteredRole_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.Value.Roles.Should().ContainSingle(role => role == Role.Registered);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Create_ShouldAddTrainerRole_WhenItSucceeds(CreateUserRequest data)
    {
        // Arrange
        // Act
        Result<User> user = User.Create(data.Id, data.IdentityProviderId, data.Email, data.FirstName, data.LastName);

        // Assert
        user.Value.Roles.Should().ContainSingle(role => role == Role.Trainer);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Change_ShouldChangeBasicInformation(CreateUserRequest data)
    {
        // Arrange
        User user = User.Create(data.Id, data.IdentityProviderId, data.Email, string.Empty, string.Empty).Value;

        // Act
        user.Change(data.FirstName, data.LastName);

        // Assert
        user.FirstName.Should().Be(data.FirstName);
        user.LastName.Should().Be(data.LastName);
    }

    [Theory]
    [ClassData(typeof(CreateUserRequestData))]
    public void Change_ShouldRaiseUserChangedDomainEvent(CreateUserRequest data)
    {
        // Arrange
        User user = User.Create(data.Id, data.IdentityProviderId, data.Email, string.Empty, string.Empty).Value;
        user.ClearDomainEvents();

        // Act
        user.Change(data.FirstName, data.LastName);

        // Assert
        AndWhichConstraint<ObjectAssertions, UserChangedDomainEvent> domainEvent = user
            .GetDomainEvents()
            .Single()
            .Should()
            .BeOfType<UserChangedDomainEvent>();

        domainEvent.Which.UserId.Should().Be(data.Id);
        domainEvent.Which.FirstName.Should().Be(data.FirstName);
        domainEvent.Which.LastName.Should().Be(data.LastName);
    }
}
