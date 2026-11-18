using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Logout;

internal sealed record LogoutCommand(string? RefreshToken) : ICommand<Unit>;

internal sealed class LogoutHandler(RefreshTokenRepository refreshTokens, TimeProvider timeProvider) : ICommandHandler<LogoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(LogoutCommand command, CancellationToken cancellationToken)
    {
        if (command.RefreshToken is not null)
        {
            RefreshTokenRecord? token = await refreshTokens.FindByHashAsync(SecureTokens.Hash(command.RefreshToken), cancellationToken);

            if (token is not null)
            {
                await refreshTokens.RevokeAsync(token.Id, timeProvider.UtcNow(), cancellationToken);
            }
        }

        return Result.Success();
    }
}

internal sealed class LogoutEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(
                "/api/auth/logout",
                async (ICommandHandler<LogoutCommand, Unit> handler, RefreshCookies cookies, HttpContext http, CancellationToken cancellationToken) =>
                {
                    Result<Unit> result = await handler.Handle(new LogoutCommand(RefreshCookies.Read(http)), cancellationToken);
                    cookies.Delete(http);
                    return result.ToNoContent();
                })
            .WithTags("Auth")
            .WithName("Logout")
            .AllowAnonymous();
}
