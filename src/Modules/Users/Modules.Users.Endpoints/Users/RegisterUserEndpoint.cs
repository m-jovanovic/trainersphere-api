using Ardalis.ApiEndpoints;
using Domain.Results;
using Endpoints.Extensions;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;
using Modules.Users.Endpoints.Constants.Routes;
using Modules.Users.Endpoints.Contracts.Users;
using Swashbuckle.AspNetCore.Annotations;

namespace Modules.Users.Endpoints.Users;

public sealed class RegisterUserEndpoint : EndpointBaseAsync
    .WithRequest<RegisterUserRequest>
    .WithActionResult
{
    private readonly ISender _sender;

    public RegisterUserEndpoint(ISender sender) => _sender = sender;

    [HttpPost(UsersRoutes.Register)]
    [SwaggerOperation(
        Summary = "Registers a new user.",
        Description = "Registers a new user based on the specified request.",
        Tags = new[] { UsersRoutes.Tag })]
    public override async Task<ActionResult> HandleAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
        await Result.Create(request)
            .Map(registerUserRequest => new RegisterUserCommand(
                string.Empty,
                registerUserRequest.FirstName,
                registerUserRequest.LastName,
                registerUserRequest.Email))
            .Bind(command => _sender.Send(command, cancellationToken))
            .Match(userId => Ok(userId), this.HandleFailure);
}
