using FluentValidation;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Me;

internal sealed record GetMeQuery(Guid UserId) : IQuery<UserResponse>;

internal sealed class GetMeHandler(UserRepository users) : IQueryHandler<GetMeQuery, UserResponse>
{
    public Task<Result<UserResponse>> Handle(GetMeQuery query, CancellationToken cancellationToken) =>
        users.GetByIdAsync(query.UserId, cancellationToken)
            .ToResult(UserErrors.NotFound(query.UserId))
            .Map(UserResponse.From);
}

internal sealed record UpdateMeRequest(string FirstName, string LastName, Units Units, string TimeZone);

internal sealed record UpdateMeCommand(Guid UserId, string FirstName, string LastName, Units Units, string TimeZone) : ICommand<UserResponse>;

internal sealed class UpdateMeValidator : AbstractValidator<UpdateMeCommand>
{
    public UpdateMeValidator()
    {
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.Units).IsInEnum();
        RuleFor(c => c.TimeZone).NotEmpty().ValidTimeZone();
    }
}

internal sealed class UpdateMeHandler(UserRepository users, IEventBus eventBus, TimeProvider timeProvider) : ICommandHandler<UpdateMeCommand, UserResponse>
{
    public Task<Result<UserResponse>> Handle(UpdateMeCommand command, CancellationToken cancellationToken) =>
        users.GetByIdAsync(command.UserId, cancellationToken)
            .ToResult(UserErrors.NotFound(command.UserId))
            .Tap(user => user.UpdateProfile(command.FirstName, command.LastName, command.Units, command.TimeZone, timeProvider.UtcNow()))
            .Tap(user => users.UpdateAsync(user, cancellationToken))
            .Tap(user => eventBus.PublishAsync(
                new UserProfileUpdated(Ids.New(timeProvider), user.UpdatedAt, user.Id, user.FirstName, user.LastName, user.Units.ToKebab(), user.TimeZone),
                cancellationToken))
            .Map(UserResponse.From);
}

internal sealed class MeEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapGet(
                "/api/auth/me",
                (IQueryHandler<GetMeQuery, UserResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetMeQuery(http.User.GetUserId()), cancellationToken).ToOk())
            .WithTags("Auth")
            .WithName("GetMe")
            .RequireAuthorization();

        app.MapPut(
                "/api/auth/me",
                (UpdateMeRequest request, ICommandHandler<UpdateMeCommand, UserResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateMeCommand(http.User.GetUserId(), request.FirstName, request.LastName, request.Units, request.TimeZone), cancellationToken).ToOk())
            .WithTags("Auth")
            .WithName("UpdateMe")
            .RequireAuthorization();
    }
}
