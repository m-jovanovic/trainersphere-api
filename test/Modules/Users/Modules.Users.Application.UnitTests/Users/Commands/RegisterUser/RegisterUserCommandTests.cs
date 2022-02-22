using System;
using System.Threading;
using System.Threading.Tasks;
using Domain.Results;
using FluentAssertions;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;
using Modules.Users.Application.Users.Commands.RegisterUser;
using Modules.Users.Domain;
using Modules.Users.Domain.Users;
using Moq;
using Xunit;

namespace Modules.Users.Application.UnitTests.Users.Commands.RegisterUser;

public class RegisterUserCommandTests
{
    [Fact]
    public async Task Handle_ShouldFail_WhenIdentityDoesNotExist()
    {
        // Arrange
        var command = new RegisterUserCommand(string.Empty, string.Empty, string.Empty, string.Empty);

        var identityProviderServiceMock = new Mock<IIdentityProviderService>();
        identityProviderServiceMock
            .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(UserErrors.IdentityNotFound));

        var handler = new RegisterUserCommandHandler(
            identityProviderServiceMock.Object,
            Mock.Of<IUserRepository>(),
            Mock.Of<IUnitOfWork>());

        // Act
        Result<Guid> result = await handler.Handle(command, default);

        // Assert
        result.Error.Should().Be(UserErrors.IdentityNotFound);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenEmailIsNotUnique()
    {
        // Arrange
        var command = new RegisterUserCommand(string.Empty, string.Empty, string.Empty, string.Empty);

        var identityProviderServiceMock = new Mock<IIdentityProviderService>();
        identityProviderServiceMock
            .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.IsEmailUniqueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure(UserErrors.EmailIsNotUnique));

        var handler = new RegisterUserCommandHandler(
            identityProviderServiceMock.Object,
            userRepositoryMock.Object,
            Mock.Of<IUnitOfWork>());

        // Act
        Result<Guid> result = await handler.Handle(command, default);

        // Assert
        result.Error.Should().Be(UserErrors.EmailIsNotUnique);
    }

    [Fact]
    public async Task Handle_ShouldSucceed_WhenIdentityExistsAndEmailIsUnique()
    {
        // Arrange
        var command = new RegisterUserCommand(string.Empty, string.Empty, string.Empty, string.Empty);

        var identityProviderServiceMock = new Mock<IIdentityProviderService>();
        identityProviderServiceMock
            .Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        var userRepositoryMock = new Mock<IUserRepository>();
        userRepositoryMock
            .Setup(x => x.IsEmailUniqueAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success);

        var handler = new RegisterUserCommandHandler(
            identityProviderServiceMock.Object,
            userRepositoryMock.Object,
            Mock.Of<IUnitOfWork>());

        // Act
        Result<Guid> result = await handler.Handle(command, default);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().NotBeEmpty();
    }
}
