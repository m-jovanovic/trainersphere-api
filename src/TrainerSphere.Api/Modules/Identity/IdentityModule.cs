using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.Api.Modules.Identity;

internal sealed class IdentityModule : IModule
{
    public const string SchemaName = "identity";

    public string Name => "Identity";

    public string Schema => SchemaName;

    public MigrationSource Migrations { get; } = new(SchemaName, typeof(IdentityModule).Assembly, "TrainerSphere.Api.Modules.Identity.Migrations");

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<AuthOptions>()
            .Bind(configuration.GetSection(AuthOptions.SectionName))
            .Validate(options => options.SigningKey.Length >= 32, "Auth:SigningKey must be at least 32 characters.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<AuthOptions>>((bearer, auth) =>
            {
                bearer.MapInboundClaims = false;
                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = auth.Value.Issuer,
                    ValidAudience = auth.Value.Audience,
                    IssuerSigningKey = auth.Value.CreateSigningKey(),
                    NameClaimType = ClaimNames.Name,
                    RoleClaimType = ClaimNames.Role,
                    ClockSkew = TimeSpan.FromSeconds(30),
                };
            });

        services.AddSingleton<PasswordHasher>();
        services.AddSingleton<AccessTokenIssuer>();
        services.AddSingleton<RefreshCookies>();
        services.AddScoped<AuthSessionIssuer>();
        services.AddScoped<UserRepository>();
        services.AddScoped<RefreshTokenRepository>();
        services.AddScoped<InvitationRepository>();
        services.AddScoped<PasswordResetRepository>();
    }
}
