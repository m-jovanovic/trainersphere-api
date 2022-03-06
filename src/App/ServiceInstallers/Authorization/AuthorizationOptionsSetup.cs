using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace App.ServiceInstallers.Authorization;

/// <summary>
/// Represents the <see cref="AuthorizationOptions"/> setup.
/// </summary>
internal sealed class AuthorizationOptionsSetup : IConfigureOptions<AuthorizationOptions>
{
    /// <inheritdoc />
    public void Configure(AuthorizationOptions options)
    {
        // Add more policies here when needed.
        // options.AddPolicy()
    }
}
