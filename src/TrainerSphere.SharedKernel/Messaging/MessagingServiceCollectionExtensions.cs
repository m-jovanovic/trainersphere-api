using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Outbox;

namespace TrainerSphere.SharedKernel.Messaging;

public static class MessagingServiceCollectionExtensions
{
    /// <summary>
    /// Registers every command/query handler, domain event handler and integration event handler found in the
    /// assembly under the namespace prefix, wrapping commands as logging -> validation -> transaction -> handler
    /// and queries as logging -> validation -> handler.
    /// </summary>
    public static IServiceCollection AddModuleHandlers(
        this IServiceCollection services,
        Assembly assembly,
        string namespacePrefix,
        string schema)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        MessageHandlerRegistry registry = GetOrAddRegistry(services);
        MessageTypeRegistry typeRegistry = GetOrAddTypeRegistry(services);

        IEnumerable<Type> types = assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false, IsGenericTypeDefinition: false } &&
                        t.Namespace is not null &&
                        (t.Namespace == namespacePrefix || t.Namespace.StartsWith(namespacePrefix + ".", StringComparison.Ordinal)));

        foreach (Type type in types)
        {
            if (typeof(IDomainEvent).IsAssignableFrom(type) || typeof(IIntegrationEvent).IsAssignableFrom(type))
            {
                typeRegistry.Register(type);
            }

            foreach (Type contract in type.GetInterfaces().Where(i => i.IsGenericType))
            {
                Type definition = contract.GetGenericTypeDefinition();

                if (definition == typeof(ICommandHandler<,>))
                {
                    RegisterCommandHandler(services, type, contract);
                }
                else if (definition == typeof(IQueryHandler<,>))
                {
                    RegisterQueryHandler(services, type, contract);
                }
                else if (definition == typeof(IDomainEventHandler<>))
                {
                    services.AddScoped(type);
                    Type eventType = contract.GetGenericArguments()[0];
                    typeRegistry.Register(eventType);
                    registry.Add(new MessageHandlerRegistration(schema, MessageKind.Outbox, eventType, type));
                }
                else if (definition == typeof(IIntegrationEventHandler<>))
                {
                    services.AddScoped(type);
                    Type eventType = contract.GetGenericArguments()[0];
                    typeRegistry.Register(eventType);
                    registry.Add(new MessageHandlerRegistration(schema, MessageKind.Inbox, eventType, type));
                }
            }
        }

        return services;
    }

    public static IServiceCollection AddMessagingCore(this IServiceCollection services)
    {
        GetOrAddRegistry(services);
        GetOrAddTypeRegistry(services);
        services.AddScoped<IDomainEventCollector, DomainEventCollector>();
        services.AddScoped<IEventBus, OutboxEventBus>();
        services.AddSingleton<MessageSignals>();
        return services;
    }

    private static void RegisterCommandHandler(IServiceCollection services, Type handlerType, Type contract)
    {
        Type[] args = contract.GetGenericArguments();
        services.AddScoped(handlerType);
        services.AddScoped(contract, sp =>
        {
            object handler = sp.GetRequiredService(handlerType);
            object transactional = ActivatorUtilities.CreateInstance(sp, typeof(TransactionalCommandDecorator<,>).MakeGenericType(args), handler);
            object validated = ActivatorUtilities.CreateInstance(sp, typeof(ValidationCommandDecorator<,>).MakeGenericType(args), transactional);
            return ActivatorUtilities.CreateInstance(sp, typeof(LoggingCommandDecorator<,>).MakeGenericType(args), validated);
        });
    }

    private static void RegisterQueryHandler(IServiceCollection services, Type handlerType, Type contract)
    {
        Type[] args = contract.GetGenericArguments();
        services.AddScoped(handlerType);
        services.AddScoped(contract, sp =>
        {
            object handler = sp.GetRequiredService(handlerType);
            object validated = ActivatorUtilities.CreateInstance(sp, typeof(ValidationQueryDecorator<,>).MakeGenericType(args), handler);
            return ActivatorUtilities.CreateInstance(sp, typeof(LoggingQueryDecorator<,>).MakeGenericType(args), validated);
        });
    }

    private static MessageHandlerRegistry GetOrAddRegistry(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(MessageHandlerRegistry))?.ImplementationInstance is MessageHandlerRegistry existing)
        {
            return existing;
        }

        MessageHandlerRegistry registry = new();
        services.AddSingleton(registry);
        return registry;
    }

    private static MessageTypeRegistry GetOrAddTypeRegistry(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(MessageTypeRegistry))?.ImplementationInstance is MessageTypeRegistry existing)
        {
            return existing;
        }

        MessageTypeRegistry registry = new();
        services.AddSingleton(registry);
        return registry;
    }
}
