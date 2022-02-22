using FluentValidation;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

/// <summary>
/// Represents the <see cref="RegisterUserCommand"/> validator.
/// </summary>
internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RegisterUserCommandValidator"/> class.
    /// </summary>
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.IdentityProviderId).NotEmpty()
            .WithErrorCode("Validation.IdentityProviderId");

        RuleFor(x => x.FirstName).NotEmpty()
            .WithErrorCode("Validation.FirstName");

        RuleFor(x => x.LastName).NotEmpty()
            .WithErrorCode("Validation.LastName");

        RuleFor(x => x.Email).NotEmpty()
            .WithErrorCode("Validation.Email");
    }
}
