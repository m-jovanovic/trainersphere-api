using FluentValidation;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Login;

internal sealed record LoginRequest(string Email, string Password);

internal sealed record LoginCommand(string Email, string Password) : ICommand<AuthSession>;

internal sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(c => c.Email).NotEmpty();
        RuleFor(c => c.Password).NotEmpty();
    }
}

internal sealed class LoginHandler(UserRepository users, PasswordHasher hasher, AuthSessionIssuer sessions) : ICommandHandler<LoginCommand, AuthSession>
{
    public async Task<Result<AuthSession>> Handle(LoginCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByEmailAsync(EmailAddress.Normalize(command.Email), cancellationToken);

        if (user is null)
        {
            hasher.VerifyDummy(command.Password);
            return AuthErrors.InvalidCredentials;
        }

        if (!hasher.Verify(user.PasswordHash, command.Password))
        {
            return AuthErrors.InvalidCredentials;
        }

        return await sessions.IssueAsync(user, familyId: null, cancellationToken);
    }
}

internal sealed class LoginEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(
                "/api/auth/login",
                async (LoginRequest request, ICommandHandler<LoginCommand, AuthSession> handler, RefreshCookies cookies, HttpContext http, CancellationToken cancellationToken) =>
                    (await handler.Handle(new LoginCommand(request.Email, request.Password), cancellationToken))
                        .ToHttpResult(session =>
                        {
                            cookies.Append(http, session.RefreshToken, session.RefreshTokenExpiresAt);
                            return TypedResults.Ok(session.Response);
                        }))
            .WithTags("Auth")
            .WithName("Login")
            .AllowAnonymous()
            .RequireRateLimiting(HostingExtensions.AuthRateLimitPolicy);
}
