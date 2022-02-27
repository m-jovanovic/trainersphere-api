using Application.Behaviors;
using FluentValidation;
using Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the user's module application service installer.
/// </summary>
internal sealed class ApplicationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services) =>
        services
            .AddMediatR(Application.AssemblyReference.Assembly)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true);
}
