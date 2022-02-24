using Infrastructure.Configuration;

namespace App.ServiceInstallers.Authorization;

/// <summary>
/// Represents the authorization service installer.
/// </summary>
internal sealed class AuthorizationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) => services.AddAuthorization();
}
