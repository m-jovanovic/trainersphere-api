namespace App.ServiceInstallation;

internal interface IServiceInstaller
{
    /// <summary>
    /// Installs the required services using the specified service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    internal void InstallService(IServiceCollection services);
}
