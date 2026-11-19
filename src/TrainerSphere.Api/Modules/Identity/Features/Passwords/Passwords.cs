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

namespace TrainerSphere.Api.Modules.Identity.Features.Passwords;

internal sealed record ChangePasswordRequest(string CurrentPassword, string NewPassword);

internal sealed record ChangePasswordCommand(Guid UserId, string CurrentPassword, string NewPassword) : ICommand<Unit>;

internal sealed class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    public ChangePasswordValidator()
    {
        RuleFor(c => c.CurrentPassword).NotEmpty();
        RuleFor(c => c.NewPassword).ValidPassword();
    }
}

internal sealed class ChangePasswordHandler(UserRepository users, RefreshTokenRepository refreshTokens, PasswordHasher hasher, TimeProvider timeProvider)
    : ICommandHandler<ChangePasswordCommand, Unit>
{
    public Task<Result<Unit>> Handle(ChangePasswordCommand command, CancellationToken cancellationToken) =>
        users.GetByIdAsync(command.UserId, cancellationToken)
            .ToResult(UserErrors.NotFound(command.UserId))
            .Ensure(user => hasher.Verify(user.PasswordHash, command.CurrentPassword), AuthErrors.InvalidCurrentPassword)
            .Tap(user => user.ChangePasswordHash(hasher.Hash(command.NewPassword), timeProvider.UtcNow()))
            .Tap(user => users.UpdateAsync(user, cancellationToken))
            .Tap(user => refreshTokens.RevokeAllForUserAsync(user.Id, user.UpdatedAt, cancellationToken))
            .Map(_ => default(Unit));
}

internal sealed record ForgotPasswordRequest(string Email);

internal sealed record ForgotPasswordCommand(string Email) : ICommand<Unit>;

internal sealed class ForgotPasswordValidator : AbstractValidator<ForgotPasswordCommand>
{
    public ForgotPasswordValidator() => RuleFor(c => c.Email).ValidEmail();
}

/// <summary>
/// Always succeeds so the response does not reveal whether an account exists.
/// </summary>
internal sealed class ForgotPasswordHandler(UserRepository users, PasswordResetRepository resets, IEventBus eventBus, TimeProvider timeProvider)
    : ICommandHandler<ForgotPasswordCommand, Unit>
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public async Task<Result<Unit>> Handle(ForgotPasswordCommand command, CancellationToken cancellationToken)
    {
        User? user = await users.GetByEmailAsync(EmailAddress.Normalize(command.Email), cancellationToken);

        if (user is not null)
        {
            DateTime now = timeProvider.UtcNow();
            string token = SecureTokens.Create();
            DateTime expiresAt = now + Lifetime;

            await resets.AddAsync(Ids.New(timeProvider), user.Id, SecureTokens.Hash(token), now, expiresAt, cancellationToken);
            await eventBus.PublishAsync(
                new PasswordResetRequested(Ids.New(timeProvider), now, user.Id, user.Email, user.FirstName, token, expiresAt),
                cancellationToken);
        }

        return Result.Success();
    }
}

internal sealed record ResetPasswordRequest(string Token, string NewPassword);

internal sealed record ResetPasswordCommand(string Token, string NewPassword) : ICommand<Unit>;

internal sealed class ResetPasswordValidator : AbstractValidator<ResetPasswordCommand>
{
    public ResetPasswordValidator()
    {
        RuleFor(c => c.Token).NotEmpty();
        RuleFor(c => c.NewPassword).ValidPassword();
    }
}

internal sealed class ResetPasswordHandler(
    PasswordResetRepository resets,
    UserRepository users,
    RefreshTokenRepository refreshTokens,
    PasswordHasher hasher,
    TimeProvider timeProvider) : ICommandHandler<ResetPasswordCommand, Unit>
{
    public async Task<Result<Unit>> Handle(ResetPasswordCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();
        PasswordResetRecord? reset = await resets.FindByHashAsync(SecureTokens.Hash(command.Token), cancellationToken);

        if (reset is null || reset.UsedAt is not null || reset.ExpiresAt <= now)
        {
            return AuthErrors.InvalidResetToken;
        }

        User? user = await users.GetByIdAsync(reset.UserId, cancellationToken);

        if (user is null)
        {
            return AuthErrors.InvalidResetToken;
        }

        user.ChangePasswordHash(hasher.Hash(command.NewPassword), now);
        await users.UpdateAsync(user, cancellationToken);
        await resets.UseAllForUserAsync(user.Id, now, cancellationToken);
        await refreshTokens.RevokeAllForUserAsync(user.Id, now, cancellationToken);

        return Result.Success();
    }
}

internal sealed class PasswordEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        app.MapPost(
                "/api/auth/change-password",
                (ChangePasswordRequest request, ICommandHandler<ChangePasswordCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ChangePasswordCommand(http.User.GetUserId(), request.CurrentPassword, request.NewPassword), cancellationToken).ToNoContent())
            .WithTags("Auth")
            .WithName("ChangePassword")
            .RequireAuthorization();

        app.MapPost(
                "/api/auth/forgot-password",
                (ForgotPasswordRequest request, ICommandHandler<ForgotPasswordCommand, Unit> handler, CancellationToken cancellationToken) =>
                    handler.Handle(new ForgotPasswordCommand(request.Email), cancellationToken).ToHttpResult(_ => TypedResults.StatusCode(StatusCodes.Status202Accepted)))
            .WithTags("Auth")
            .WithName("ForgotPassword")
            .AllowAnonymous()
            .RequireRateLimiting(HostingExtensions.AuthRateLimitPolicy);

        app.MapPost(
                "/api/auth/reset-password",
                (ResetPasswordRequest request, ICommandHandler<ResetPasswordCommand, Unit> handler, CancellationToken cancellationToken) =>
                    handler.Handle(new ResetPasswordCommand(request.Token, request.NewPassword), cancellationToken).ToNoContent())
            .WithTags("Auth")
            .WithName("ResetPassword")
            .AllowAnonymous()
            .RequireRateLimiting(HostingExtensions.AuthRateLimitPolicy);
    }
}
