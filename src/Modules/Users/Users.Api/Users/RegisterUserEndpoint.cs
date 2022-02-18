using Ardalis.ApiEndpoints;
using Domain.Results;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Users.Api.Constants.Routes;
using Users.Api.Contracts.Users;
using Users.Api.Extensions;
using Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Users.Api.Users;

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
                registerUserRequest.IdentityProviderId,
                registerUserRequest.FirstName,
                registerUserRequest.LastName,
                registerUserRequest.Email))
            .Bind(command => _sender.Send(command, cancellationToken))
            .Match(Ok, BadRequest);
}
