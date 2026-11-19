using Microsoft.Extensions.Options;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Identity.Infrastructure;

internal sealed record UserResponse(
    Guid Id,
    string Email,
    string FirstName,
    string LastName,
    UserRole Role,
    Guid? TrainerId,
    Units Units,
    string TimeZone,
    DateTime CreatedAt)
{
    public static UserResponse From(User user) =>
        new(user.Id, user.Email, user.FirstName, user.LastName, user.Role, user.TrainerId, user.Units, user.TimeZone, user.CreatedAt);
}

internal sealed record AuthResponse(string AccessToken, string TokenType, int ExpiresIn, UserResponse User);

/// <summary>
/// The outcome of a successful sign-in: the response body plus the refresh token that goes into the cookie.
/// </summary>
internal sealed record AuthSession(AuthResponse Response, Guid RefreshTokenId, string RefreshToken, DateTime RefreshTokenExpiresAt);

internal sealed class AuthSessionIssuer(
    AccessTokenIssuer accessTokens,
    RefreshTokenRepository refreshTokens,
    IOptions<AuthOptions> options,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Issues an access token and a new refresh token. Pass the family of the rotated token on refresh; a new login starts a new family.
    /// </summary>
    public async Task<AuthSession> IssueAsync(User user, Guid? familyId, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();
        Guid tokenId = Ids.New(timeProvider);
        string token = SecureTokens.Create();
        DateTime expiresAt = now.AddDays(options.Value.RefreshTokenDays);

        await refreshTokens.AddAsync(
            new RefreshTokenRecord
            {
                Id = tokenId,
                UserId = user.Id,
                FamilyId = familyId ?? tokenId,
                TokenHash = SecureTokens.Hash(token),
                CreatedAt = now,
                ExpiresAt = expiresAt,
            },
            cancellationToken);

        AuthResponse response = new(accessTokens.Issue(user), "Bearer", accessTokens.LifetimeSeconds, UserResponse.From(user));
        return new AuthSession(response, tokenId, token, expiresAt);
    }
}
