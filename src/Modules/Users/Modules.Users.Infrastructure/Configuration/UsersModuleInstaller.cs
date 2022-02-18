using Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Infrastructure.Configuration;

/// <summary>
/// Represents the users module installer.
/// </summary>
public sealed class UsersModuleInstaller : IModuleInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services)
    {
        services.AddMediatR(Application.AssemblyReference.Assembly);

        services.AddControllers().AddApplicationPart(Endpoints.AssemblyReference.Assembly);
    }
}
