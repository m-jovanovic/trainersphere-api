using Infrastructure.Configuration;
using Infrastructure.Utilities;
using MediatR;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Modules.Notifications.Infrastructure.Idempotence;
using Shared.Extensions;

namespace Modules.Notifications.Infrastructure.ServiceInstallers;

/// <summary>
/// Represents the notifications module application service installer.
/// </summary>
internal sealed class ApplicationServiceInstaller : IServiceInstaller
{
    /// <inheritdoc />
    public void Install(IServiceCollection services, IConfiguration configuration) =>
        services
            .AddMediatR(Application.AssemblyReference.Assembly)
            .Tap(DecorateIntegrationEventHandlersWithIdempotency);

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
