using Application.EventBus;
using Infrastructure.Configuration;
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
            .Where(type =>
                type.GetInterfaces().Any(
                    interfaceType => IsNotificationHandler(interfaceType) || IsIntegrationEventHandler(interfaceType)))
            .ForEach(type =>
            {
                Type closedNotificationHandler = type.GetInterfaces().First(IsNotificationHandler);

                Type[] arguments = closedNotificationHandler.GetGenericArguments();

                Type closedIdempotentHandler = typeof(IdempotentIntegrationEventHandler<>).MakeGenericType(arguments);

                services.Decorate(closedNotificationHandler, closedIdempotentHandler);
            });

    private static bool IsNotificationHandler(Type type) =>
        type.IsGenericType &&
        type.Name.StartsWith(typeof(INotificationHandler<>).Name, StringComparison.InvariantCulture);

    private static bool IsIntegrationEventHandler(Type type) =>
        type.IsGenericType &&
        type.Name.StartsWith(typeof(IIntegrationEventHandler<>).Name, StringComparison.InvariantCulture);
}
