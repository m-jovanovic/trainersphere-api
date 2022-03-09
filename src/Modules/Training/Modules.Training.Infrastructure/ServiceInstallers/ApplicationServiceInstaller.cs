using Application.Behaviors;
using FluentValidation;
using Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Modules.Training.Infrastructure.ServiceInstallers;

/// <summary>
/// Represents the users module application service installer.
/// </summary>
internal sealed class ApplicationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .AddMediatR(Application.AssemblyReference.Assembly)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true);
}
