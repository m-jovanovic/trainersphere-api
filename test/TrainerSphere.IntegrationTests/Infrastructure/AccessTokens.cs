using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Reads access tokens the API issued and forges tokens for authentication edge cases (expired, wrong key).
/// </summary>
public static class AccessTokens
{
    public const string Issuer = "trainersphere";
    public const string Audience = "trainersphere";

    public static JsonWebToken Read(string accessToken) => new(accessToken);

    public static string? Claim(this JsonWebToken token, string type)
    {
        ArgumentNullException.ThrowIfNull(token);

        return token.TryGetClaim(type, out Claim? claim) ? claim.Value : null;
    }

    public static string Create(
        Guid userId,
        string role,
        DateTime? expires = null,
        string signingKey = ApiFactory.SigningKey,
        string issuer = Issuer,
        string audience = Audience,
        string email = "forged@test.trainersphere.dev")
    {
        DateTime expiresAt = expires ?? DateTime.UtcNow.AddMinutes(15);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            IssuedAt = expiresAt.AddMinutes(-15),
            NotBefore = expiresAt.AddMinutes(-15),
            Expires = expiresAt,
            Claims = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["sub"] = userId.ToString(),
                ["email"] = email,
                ["role"] = role,
                ["name"] = "Forged User",
            },
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }

    /// <summary>
    /// An unsigned token (<c>alg: none</c>) with otherwise valid claims.
    /// </summary>
    public static string CreateUnsigned(Guid userId, string role)
    {
        string signed = Create(userId, role);
        string[] parts = signed.Split('.');
        string header = Base64UrlEncoder.Encode("""{"alg":"none","typ":"JWT"}""");
        return $"{header}.{parts[1]}.";
    }
}
