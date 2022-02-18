using FluentValidation;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

internal sealed class RegisterUserCommandValidator : AbstractValidator<RegisterUserCommand>
{
    public RegisterUserCommandValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty();
        RuleFor(x => x.LastName).NotEmpty();
        RuleFor(x => x.Email).NotEmpty();
    }
}
