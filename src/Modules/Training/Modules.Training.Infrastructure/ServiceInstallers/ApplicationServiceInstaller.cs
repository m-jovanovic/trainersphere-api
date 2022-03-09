using Application.Behaviors;
using FluentValidation;
using Infrastructure.Configuration;
using Infrastructure.Utilities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Training.Infrastructure.Idempotence;
using Shared.Extensions;

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
            .AddValidatorsFromAssembly(Application.AssemblyReference.Assembly, includeInternalTypes: true)
            .Tap(DecorateDomainEventHandlersWithIdempotency)
            .Tap(DecorateIntegrationEventHandlersWithIdempotency);

    private static void DecorateDomainEventHandlersWithIdempotency(IServiceCollection services) =>
        Application.AssemblyReference.Assembly
            .GetTypes()
            .Where(NotificationHandlerUtility.ImplementsDomainEventHandler)
            .ForEach(type =>
            {
                Type closedNotificationHandler = type.GetInterfaces().First(NotificationHandlerUtility.IsNotificationHandler);

                Type[] arguments = closedNotificationHandler.GetGenericArguments();

                Type closedIdempotentHandler = typeof(IdempotentDomainEventHandler<>).MakeGenericType(arguments);

                services.Decorate(closedNotificationHandler, closedIdempotentHandler);
            });

    private static void DecorateIntegrationEventHandlersWithIdempotency(IServiceCollection services) =>
        Application.AssemblyReference.Assembly
            .GetTypes()
            .Where(NotificationHandlerUtility.ImplementsIntegrationEventHandler)
            .ForEach(type =>
            {
                Type closedNotificationHandler = type.GetInterfaces().First(NotificationHandlerUtility.IsNotificationHandler);

                Type[] arguments = closedNotificationHandler.GetGenericArguments();

                Type closedIdempotentHandler = typeof(IdempotentIntegrationEventHandler<>).MakeGenericType(arguments);

                services.Decorate(closedNotificationHandler, closedIdempotentHandler);
            });
}
