using Ardalis.ApiEndpoints;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Users.Api.Users;

public sealed class RegisterUserEndpoint : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult
{
    private readonly ISender _sender;

    public RegisterUserEndpoint(ISender sender) => _sender = sender;

    [HttpPost(Routes.Register)]
    [SwaggerOperation(
        Summary = "Registers a new user.",
        Description = "Registers a new user based on the specified request.",
        Tags = new[] { Routes.Tag })]
    public override async Task<ActionResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        await _sender.Send(new RegisterUserCommand(string.Empty, string.Empty, string.Empty, string.Empty), cancellationToken);

        return Ok();
    }
}
