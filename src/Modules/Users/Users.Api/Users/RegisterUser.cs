using Ardalis.ApiEndpoints;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Users.Application.Boundary.Users.Commands.RegisterUser;

namespace Users.Api.Users;

public sealed class RegisterUser : EndpointBaseAsync
    .WithoutRequest
    .WithActionResult
{
    private readonly ISender _sender;

    public RegisterUser(ISender sender) => _sender = sender;

    [HttpPost("users")]
    [SwaggerOperation(Tags = new[] { "Users" })]
    public override async Task<ActionResult> HandleAsync(CancellationToken cancellationToken = default)
    {
        await _sender.Send(new RegisterUserCommand(string.Empty, string.Empty, string.Empty, string.Empty), cancellationToken);

        return Ok();
    }
}
