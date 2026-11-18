using FluentValidation;
using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Identity.Features.Signup;

internal sealed record SignupRequest(string Email, string Password, string FirstName, string LastName, string? TimeZone);

internal sealed record SignupCommand(string Email, string Password, string FirstName, string LastName, string? TimeZone) : ICommand<AuthSession>;

internal sealed class SignupValidator : AbstractValidator<SignupCommand>
{
    public SignupValidator()
    {
        RuleFor(c => c.Email).ValidEmail();
        RuleFor(c => c.Password).ValidPassword();
        RuleFor(c => c.FirstName).ValidName();
        RuleFor(c => c.LastName).ValidName();
        RuleFor(c => c.TimeZone).ValidTimeZone();
    }
}

internal sealed class SignupHandler(
    UserRepository users,
    PasswordHasher hasher,
    AuthSessionIssuer sessions,
    IEventBus eventBus,
    TimeProvider timeProvider) : ICommandHandler<SignupCommand, AuthSession>
{
    public async Task<Result<AuthSession>> Handle(SignupCommand command, CancellationToken cancellationToken)
    {
        string email = EmailAddress.Normalize(command.Email);
        DateTime now = timeProvider.UtcNow();
        var user = User.RegisterTrainer(Ids.New(timeProvider), email, hasher.Hash(command.Password), command.FirstName, command.LastName, command.TimeZone, now);

        if (!await users.TryAddAsync(user, cancellationToken))
        {
            return UserErrors.EmailInUse(email);
        }

        await eventBus.PublishAsync(
            new TrainerRegistered(Ids.New(timeProvider), now, user.Id, user.Email, user.FirstName, user.LastName, user.TimeZone),
            cancellationToken);

        return await sessions.IssueAsync(user, familyId: null, cancellationToken);
    }
}

internal sealed class SignupEndpoint : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app) =>
        app.MapPost(
                "/api/auth/signup",
                async (SignupRequest request, ICommandHandler<SignupCommand, AuthSession> handler, RefreshCookies cookies, HttpContext http, CancellationToken cancellationToken) =>
                    (await handler.Handle(new SignupCommand(request.Email, request.Password, request.FirstName, request.LastName, request.TimeZone), cancellationToken))
                        .ToHttpResult(session =>
                        {
                            cookies.Append(http, session.RefreshToken, session.RefreshTokenExpiresAt);
                            return TypedResults.Created("/api/auth/me", session.Response);
                        }))
            .WithTags("Auth")
            .WithName("Signup")
            .AllowAnonymous()
            .RequireRateLimiting(HostingExtensions.AuthRateLimitPolicy);
}
