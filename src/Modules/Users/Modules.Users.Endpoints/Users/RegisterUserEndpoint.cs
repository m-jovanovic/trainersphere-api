using Ardalis.ApiEndpoints;
using Domain.Results;
using Endpoints.Authorization;
using Endpoints.Extensions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Modules.Users.Application.Boundary.Users.Commands.RegisterUser;
using Modules.Users.Endpoints.Contracts.Users;
using Modules.Users.Endpoints.Routes;
using Swashbuckle.AspNetCore.Annotations;

namespace Modules.Users.Endpoints.Users;

public sealed class RegisterUserEndpoint : EndpointBaseAsync
    .WithRequest<RegisterUserRequest>
    .WithActionResult
{
    private readonly ISender _sender;

    public RegisterUserEndpoint(ISender sender) => _sender = sender;

    [HasPermission(UsersPermissions.UserModify)]
    [HttpPost(UsersRoutes.Register)]
    [ProducesResponseType(typeof(Guid), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [SwaggerOperation(
        Summary = "Registers a new user.",
        Description = "Registers a new user based on the specified request.",
        Tags = new[] { UsersRoutes.Tag })]
    public override async Task<ActionResult> HandleAsync(RegisterUserRequest request, CancellationToken cancellationToken = default) =>
        await Result.Create(request)
            .Map(registerUserRequest => new RegisterUserCommand(
                User.GetIdentityProviderId(),
                registerUserRequest.Email,
                registerUserRequest.FirstName,
                registerUserRequest.LastName))
            .Bind(command => _sender.Send(command, cancellationToken))
            .Match(userId => CreatedAtRoute(nameof(GetUserByIdEndpoint), new { userId }, userId), this.HandleFailure);
}
