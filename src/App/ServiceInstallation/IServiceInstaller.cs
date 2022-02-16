namespace App.ServiceInstallation;

/// <summary>
/// Represents the interface for installing services.
/// </summary>
internal interface IServiceInstaller
{
    /// <summary>
    /// Installs the required services using the specified service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    internal void Install(IServiceCollection services);
}
