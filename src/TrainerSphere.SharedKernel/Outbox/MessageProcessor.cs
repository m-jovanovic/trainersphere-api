using System.Collections.Concurrent;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.SharedKernel.Outbox;

public interface IMessageProcessor
{
    string Schema { get; }

    MessageKind Kind { get; }

    /// <summary>
    /// Processes one batch of pending messages and returns how many were handled (successfully or not).
    /// </summary>
    Task<int> ProcessBatchAsync(CancellationToken cancellationToken);
}

public sealed class MessagingOptions
{
    public const string SectionName = "Messaging";

    public bool Enabled { get; set; } = true;

    public int PollingIntervalMilliseconds { get; set; } = 1000;

    public int BatchSize { get; set; } = 50;

    public int MaxAttempts { get; set; } = 5;
}

/// <summary>
/// Dispatches outbox (domain events) or inbox (integration events) messages of one module schema to their handlers.
/// Each handler runs in its own scope and transaction together with its consumer record, so a message is handled
/// at most once per handler even when another handler for the same message fails and the message is retried.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Security", "S2077:Formatting SQL queries is security-sensitive", Justification = "Only validated schema/table identifiers are interpolated; values are parameters.")]
internal sealed partial class MessageProcessor : IMessageProcessor
{
    private static readonly ConcurrentDictionary<Type, MethodInfo> HandleMethods = new();

    private readonly NpgsqlDataSource _dataSource;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessageHandlerRegistry _handlers;
    private readonly MessageTypeRegistry _types;
    private readonly IOptions<MessagingOptions> _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MessageProcessor> _logger;
    private readonly string _table;

    public MessageProcessor(
        string schema,
        MessageKind kind,
        NpgsqlDataSource dataSource,
        IServiceScopeFactory scopeFactory,
        MessageHandlerRegistry handlers,
        MessageTypeRegistry types,
        IOptions<MessagingOptions> options,
        TimeProvider timeProvider,
        ILogger<MessageProcessor> logger)
    {
        SchemaName.EnsureValid(schema);

        Schema = schema;
        Kind = kind;
        _dataSource = dataSource;
        _scopeFactory = scopeFactory;
        _handlers = handlers;
        _types = types;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
        _table = kind == MessageKind.Outbox ? "outbox_messages" : "inbox_messages";
    }

    public string Schema { get; }

    public MessageKind Kind { get; }

    public async Task<int> ProcessBatchAsync(CancellationToken cancellationToken)
    {
        MessagingOptions options = _options.Value;

        await using NpgsqlConnection connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken);

        List<MessageRow> messages = (await connection.QueryAsync<MessageRow>(
            new CommandDefinition(
                $"""
                SELECT id, type, content, attempts
                FROM {Schema}.{_table}
                WHERE processed_at IS NULL AND attempts < @MaxAttempts
                ORDER BY occurred_at, id
                LIMIT @BatchSize
                FOR UPDATE SKIP LOCKED
                """,
                new { options.MaxAttempts, options.BatchSize },
                transaction,
                cancellationToken: cancellationToken))).AsList();

        foreach (MessageRow message in messages)
        {
            string? error = await DispatchAsync(connection, transaction, message, cancellationToken);

            await connection.ExecuteAsync(
                new CommandDefinition(
                    error is null
                        ? $"UPDATE {Schema}.{_table} SET processed_at = @Now, error = NULL WHERE id = @Id"
                        : $"UPDATE {Schema}.{_table} SET attempts = attempts + 1, error = @Error WHERE id = @Id",
                    new { message.Id, Now = _timeProvider.UtcNow(), Error = error },
                    transaction,
                    cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);

        return messages.Count;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Handler {Handler} failed for message {MessageId}")]
    private static partial void LogHandlerFailed(ILogger logger, Exception exception, string handler, Guid messageId);

    private static MethodInfo GetHandleMethod(Type handlerType) =>
        HandleMethods.GetOrAdd(handlerType, static t =>
            t.GetMethod("Handle", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new InvalidOperationException($"Handler {t} has no public Handle method."));

    private async Task<string?> DispatchAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        MessageRow message,
        CancellationToken cancellationToken)
    {
        Type? eventType = _types.Resolve(message.Type);

        if (eventType is null)
        {
            return $"Unknown message type '{message.Type}'.";
        }

        object? payload = MessageSerializer.Deserialize(message.Content, eventType);

        if (payload is null)
        {
            return "Message content deserialized to null.";
        }

        List<string> errors = [];

        foreach (Type handlerType in _handlers.GetHandlers(Schema, Kind, eventType))
        {
            string consumer = MessageTypeRegistry.NameOf(handlerType);

            bool alreadyHandled = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition(
                    $"SELECT EXISTS (SELECT 1 FROM {Schema}.{_table[..^1]}_consumers WHERE message_id = @Id AND handler = @Handler)",
                    new { message.Id, Handler = consumer },
                    transaction,
                    cancellationToken: cancellationToken));

            if (alreadyHandled)
            {
                continue;
            }

            string? error = await RunHandlerAsync(handlerType, consumer, payload, message.Id, cancellationToken);

            if (error is not null)
            {
                errors.Add(error);
            }
        }

        return errors.Count == 0 ? null : string.Join(Environment.NewLine, errors);
    }

    private async Task<string?> RunHandlerAsync(Type handlerType, string consumer, object payload, Guid messageId, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = _scopeFactory.CreateAsyncScope();
        IDbSession session = scope.ServiceProvider.GetRequiredService<IDbSession>();
        IDomainEventCollector collector = scope.ServiceProvider.GetRequiredService<IDomainEventCollector>();

        await session.BeginTransactionAsync(cancellationToken);

        try
        {
            object handler = scope.ServiceProvider.GetRequiredService(handlerType);
            var task = (Task)GetHandleMethod(handlerType).Invoke(handler, [payload, cancellationToken])!;
            await task;

            await collector.FlushToOutboxAsync(session, cancellationToken);
            await session.ExecuteAsync(
                $"INSERT INTO {Schema}.{_table[..^1]}_consumers (message_id, handler, processed_at) VALUES (@Id, @Handler, @Now)",
                new { Id = messageId, Handler = consumer, Now = _timeProvider.UtcNow() },
                cancellationToken);
            await session.CommitAsync(cancellationToken);

            return null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            Exception actual = ex is TargetInvocationException { InnerException: { } inner } ? inner : ex;
            await session.RollbackAsync(CancellationToken.None);
            LogHandlerFailed(_logger, actual, consumer, messageId);
            return $"{consumer}: {actual.GetType().Name}: {actual.Message}";
        }
    }

    private sealed record MessageRow(Guid Id, string Type, string Content, int Attempts);
}
