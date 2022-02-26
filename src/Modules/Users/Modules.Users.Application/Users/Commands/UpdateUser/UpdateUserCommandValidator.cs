using FluentValidation;
using Modules.Users.Application.Boundary.Users.Commands.UpdateUser;

namespace Modules.Users.Application.Users.Commands.UpdateUser;

/// <summary>
/// Represents the <see cref="UpdateUserCommand"/> validator.
/// </summary>
internal sealed class UpdateUserCommandValidator : AbstractValidator<UpdateUserCommand>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateUserCommandValidator"/> class.
    /// </summary>
    public UpdateUserCommandValidator()
    {
        RuleFor(x => x.UserId).NotEmpty();

        RuleFor(x => x.FirstName).NotEmpty();

        RuleFor(x => x.LastName).NotEmpty();
    }
}
