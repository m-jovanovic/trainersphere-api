using System.Text.Json;
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Infrastructure.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Infrastructure.Configuration.Contracts;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the Firebase service installer.
/// </summary>
internal sealed class FirebaseServiceInstaller : IServiceInstaller
{
    private const string ConfigurationSettingName = "GoogleApplicationCredentials";

    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        FirebaseApp.Create(new AppOptions
        {
            Credential = GoogleCredential.FromJson(
                JsonSerializer.Serialize(configuration.GetSection(ConfigurationSettingName).Get<FirebaseConfiguration>()))
        });
}
