using Microsoft.AspNetCore.Http.HttpResults;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Refresh;

internal sealed record RefreshCommand(string? RefreshToken) : ICommand<AuthSession>;

/// <summary>
/// Rotates the refresh token. Presenting a token that was already rotated means it leaked (or two tabs raced),
/// so the whole family is revoked and every session from that login must sign in again.
/// </summary>
internal sealed class RefreshHandler(
    RefreshTokenRepository refreshTokens,
    UserRepository users,
    AuthSessionIssuer sessions,
    TimeProvider timeProvider) : ICommandHandler<RefreshCommand, AuthSession>
{
    public async Task<Result<AuthSession>> Handle(RefreshCommand command, CancellationToken cancellationToken)
    {
        if (command.RefreshToken is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        DateTime now = timeProvider.UtcNow();
        RefreshTokenRecord? token = await refreshTokens.FindByHashAsync(SecureTokens.Hash(command.RefreshToken), cancellationToken);

        if (token is null || token.ExpiresAt <= now)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        if (token.RevokedAt is not null)
        {
            if (token.ReplacedById is not null)
            {
                await refreshTokens.RevokeFamilyImmediatelyAsync(token.FamilyId, now, cancellationToken);
            }

            return AuthErrors.InvalidRefreshToken;
        }

        User? user = await users.GetByIdAsync(token.UserId, cancellationToken);

        if (user is null)
        {
            return AuthErrors.InvalidRefreshToken;
        }

        AuthSession session = await sessions.IssueAsync(user, token.FamilyId, cancellationToken);

        if (!await refreshTokens.TryRotateAsync(token.Id, session.RefreshTokenId, now, cancellationToken))
        {
            await refreshTokens.RevokeFamilyImmediatelyAsync(token.FamilyId, now, cancellationToken);
            return AuthErrors.InvalidRefreshToken;
        }

        return session;
    }
}

internal sealed class RefreshEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(
                "/api/auth/refresh",
                async (ICommandHandler<RefreshCommand, AuthSession> handler, RefreshCookies cookies, HttpContext http, CancellationToken cancellationToken) =>
                    (await handler.Handle(new RefreshCommand(RefreshCookies.Read(http)), cancellationToken))
                        .Match<AuthSession, Results<Ok<AuthResponse>, ProblemHttpResult>>(
                            session =>
                            {
                                cookies.Append(http, session.RefreshToken, session.RefreshTokenExpiresAt);
                                return TypedResults.Ok(session.Response);
                            },
                            error =>
                            {
                                cookies.Delete(http);
                                return error.ToProblem();
                            }))
            .WithTags("Auth")
            .WithName("RefreshSession")
            .AllowAnonymous();
}
