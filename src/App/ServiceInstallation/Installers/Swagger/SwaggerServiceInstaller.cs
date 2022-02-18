using Infrastructure.Configuration;

namespace App.ServiceInstallation.Installers.Swagger;

/// <summary>
/// Represents the swagger service installer.
/// </summary>
internal sealed class SwaggerServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    void IServiceInstaller.Install(IServiceCollection services)
    {
        services.ConfigureOptions<SwaggerGenOptionsSetup>();

        services.ConfigureOptions<SwaggerUiOptionsSetup>();

        services.AddSwaggerGen();
    }
}
