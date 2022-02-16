using Application.Messaging;
using Domain.Results;
using Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Users.Application.Users.Commands.RegisterUser;

internal sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand>
{
    public Task<Result> Handle(RegisterUserCommand request, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
