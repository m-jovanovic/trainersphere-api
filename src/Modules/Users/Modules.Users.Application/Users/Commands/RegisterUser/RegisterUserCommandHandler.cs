using Application.Messaging;
using Domain.Results;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Modules.Users.Application.Users.Commands.RegisterUser;

internal sealed class RegisterUserCommandHandler : ICommandHandler<RegisterUserCommand>
{
    public Task<Result> Handle(RegisterUserCommand request, CancellationToken cancellationToken) => Task.FromResult(Result.Success());
}
