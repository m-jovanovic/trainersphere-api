using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerSphere.SharedKernel.Outbox;

public enum MessageKind
{
    Outbox,
    Inbox,
}

public static class MessageSerializer
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static string Serialize(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        return JsonSerializer.Serialize(message, message.GetType(), Options);
    }

    public static object? Deserialize(string content, Type type) => JsonSerializer.Deserialize(content, type, Options);
}

public sealed record MessageHandlerRegistration(string Schema, MessageKind Kind, Type EventType, Type HandlerType);

public sealed class MessageHandlerRegistry
{
    private readonly List<MessageHandlerRegistration> _registrations = [];

    public IReadOnlyList<MessageHandlerRegistration> Registrations => _registrations;

    public void Add(MessageHandlerRegistration registration)
    {
        if (!_registrations.Contains(registration))
        {
            _registrations.Add(registration);
        }
    }

    public IReadOnlyList<Type> GetHandlers(string schema, MessageKind kind, Type eventType) =>
        [.. _registrations.Where(r => r.Schema == schema && r.Kind == kind && r.EventType == eventType).Select(r => r.HandlerType)];

    public IReadOnlyList<string> GetSubscriberSchemas(Type integrationEventType) =>
        [.. _registrations.Where(r => r.Kind == MessageKind.Inbox && r.EventType == integrationEventType).Select(r => r.Schema).Distinct()];
}

public sealed class MessageTypeRegistry
{
    private readonly ConcurrentDictionary<string, Type> _types = new(StringComparer.Ordinal);

    public static string NameOf(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);

        return type.FullName ?? type.Name;
    }

    public void Register(Type type) => _types.TryAdd(NameOf(type), type);

    public Type? Resolve(string name) => _types.GetValueOrDefault(name);
}

/// <summary>
/// Wakes message processors as soon as new messages are committed so cross-module flows are not bound to the poll interval.
/// </summary>
public sealed class MessageSignals
{
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _signals = new(StringComparer.Ordinal);

    public void Notify(string schema, MessageKind kind)
    {
        SemaphoreSlim signal = Get(schema, kind);

        if (signal.CurrentCount == 0)
        {
            try
            {
                signal.Release();
            }
            catch (SemaphoreFullException)
            {
                // Another notifier released it between the check and the release; one wake-up is enough.
            }
        }
    }

    public async Task WaitAsync(string schema, MessageKind kind, TimeSpan timeout, CancellationToken cancellationToken) =>
        await Get(schema, kind).WaitAsync(timeout, cancellationToken);

    private SemaphoreSlim Get(string schema, MessageKind kind) =>
        _signals.GetOrAdd($"{schema}:{kind}", static _ => new SemaphoreSlim(0, 1));
}
