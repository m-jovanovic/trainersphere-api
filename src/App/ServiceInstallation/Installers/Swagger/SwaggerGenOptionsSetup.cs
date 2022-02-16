using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.SwaggerGen;

namespace App.ServiceInstallation.Installers.Swagger;

/// <summary>
/// Represents the <see cref="SwaggerGenOptions"/> setup.
/// </summary>
internal sealed class SwaggerGenOptionsSetup : IConfigureOptions<SwaggerGenOptions>
{
    /// <inheritdoc />
    public void Configure(SwaggerGenOptions options)
    {
        options.UseApiEndpoints();

        options.EnableAnnotations();
    }
}
