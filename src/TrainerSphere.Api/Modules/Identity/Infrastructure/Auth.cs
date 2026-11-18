using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.SharedKernel.Http;

namespace TrainerSphere.Api.Modules.Identity.Infrastructure;

internal static class SecureTokens
{
    /// <summary>
    /// A 256-bit random token, URL-safe base64 without padding (43 characters).
    /// </summary>
    public static string Create() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

    /// <summary>
    /// Tokens are stored only as SHA-256 hashes; they are high-entropy, so no salt or slow hash is needed.
    /// </summary>
    public static string Hash(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}

internal sealed class AuthOptions
{
    public const string SectionName = "Auth";

    public string Issuer { get; set; } = "trainersphere";

    public string Audience { get; set; } = "trainersphere";

    public string SigningKey { get; set; } = string.Empty;

    public int AccessTokenMinutes { get; set; } = 15;

    public int RefreshTokenDays { get; set; } = 14;

    /// <summary>
    /// Gets or sets a value indicating whether the refresh cookie is marked Secure; only the plain-http compose setup turns it off.
    /// </summary>
    public bool RefreshCookieSecure { get; set; } = true;

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(SigningKey));
}

/// <summary>
/// PBKDF2 password hashing from ASP.NET Core Identity (versioned format, so the work factor can be raised later).
/// </summary>
internal sealed class PasswordHasher
{
    private static readonly object Subject = new();
    private readonly PasswordHasher<object> _hasher = new();
    private readonly Lazy<string> _dummyHash;

    public PasswordHasher() => _dummyHash = new Lazy<string>(() => _hasher.HashPassword(Subject, "not-a-real-password"));

    public string Hash(string password) => _hasher.HashPassword(Subject, password);

    public bool Verify(string hash, string password) =>
        _hasher.VerifyHashedPassword(Subject, hash, password) != PasswordVerificationResult.Failed;

    /// <summary>
    /// Burns the same time as a real verification so unknown emails cannot be told apart by response time.
    /// </summary>
    public void VerifyDummy(string password) => _ = Verify(_dummyHash.Value, password);
}

internal sealed class AccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider)
{
    private readonly JsonWebTokenHandler _handler = new();

    public int LifetimeSeconds => options.Value.AccessTokenMinutes * 60;

    public string Issue(User user)
    {
        AuthOptions settings = options.Value;
        DateTime now = timeProvider.GetUtcNow().UtcDateTime;

        Dictionary<string, object> claims = new(StringComparer.Ordinal)
        {
            [ClaimNames.Subject] = user.Id.ToString(),
            [ClaimNames.Email] = user.Email,
            [ClaimNames.Role] = user.Role == UserRole.Trainer ? Roles.Trainer : Roles.Client,
            [ClaimNames.Name] = user.FullName,
        };

        if (user.TrainerId is { } trainerId)
        {
            claims[ClaimNames.TrainerId] = trainerId.ToString();
        }

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = settings.Issuer,
            Audience = settings.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(settings.AccessTokenMinutes),
            Claims = claims,
            Subject = new ClaimsIdentity(),
            SigningCredentials = new SigningCredentials(settings.CreateSigningKey(), SecurityAlgorithms.HmacSha256),
        });
    }
}

/// <summary>
/// The httpOnly refresh token cookie. Scoped to /api/auth so it is only sent to refresh and logout.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S2092:Cookies should be secure", Justification = "Secure is on by default; only the plain-http local compose setup turns it off through configuration.")]
internal sealed class RefreshCookies(IOptions<AuthOptions> options)
{
    public const string Name = "ts_refresh";
    private const string Path = "/api/auth";

    public static string? Read(HttpContext http) => http.Request.Cookies.TryGetValue(Name, out string? value) && !string.IsNullOrWhiteSpace(value) ? value : null;

    public void Append(HttpContext http, string token, DateTime expiresAt) =>
        http.Response.Cookies.Append(Name, token, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Value.RefreshCookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
            Expires = new DateTimeOffset(expiresAt, TimeSpan.Zero),
            IsEssential = true,
        });

    public void Delete(HttpContext http) =>
        http.Response.Cookies.Delete(Name, new CookieOptions
        {
            HttpOnly = true,
            Secure = options.Value.RefreshCookieSecure,
            SameSite = SameSiteMode.Strict,
            Path = Path,
        });
}
