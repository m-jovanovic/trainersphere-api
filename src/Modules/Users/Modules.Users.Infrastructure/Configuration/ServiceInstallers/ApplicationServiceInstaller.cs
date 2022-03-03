using Application.Behaviors;
using FluentValidation;
using Infrastructure.Configuration;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Infrastructure.Idempotency;
using Shared.Extensions;

namespace Modules.Users.Infrastructure.Configuration.ServiceInstallers;

/// <summary>
/// Represents the user's module application service installer.
/// </summary>
internal sealed class ApplicationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .AddMediatR(Application.AssemblyReference.Assembly)
            .AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationPipelineBehavior<,>))
            .AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true)
            .Tap(DecorateDomainEventHandlersWithIdempotency);

    private static void DecorateDomainEventHandlersWithIdempotency(IServiceCollection services) =>
        Application.AssemblyReference.Assembly
            .GetTypes()
            .Where(type => type.GetInterfaces().Any(IsNotificationHandler))
            .ForEach(type =>
            {
                Type closedNotificationHandler = type.GetInterfaces().First(IsNotificationHandler);

                Type[] arguments = closedNotificationHandler.GetGenericArguments();

                Type closedIdempotentHandler = typeof(IdempotentDomainEventHandler<>).MakeGenericType(arguments);

                services.Decorate(closedNotificationHandler, closedIdempotentHandler);
            });

    private static bool IsNotificationHandler(Type type) =>
        type.IsGenericType &&
        type.Name.StartsWith(typeof(INotificationHandler<>).Name, StringComparison.InvariantCulture);
}
