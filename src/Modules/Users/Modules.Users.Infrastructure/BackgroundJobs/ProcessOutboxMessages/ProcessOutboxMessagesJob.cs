using Application.Data;
using Application.Time;
using Domain.Primitives;
using MediatR;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Polly;
using Polly.Retry;
using Quartz;

namespace Modules.Users.Infrastructure.BackgroundJobs.ProcessOutboxMessages;

/// <summary>
/// Represents the background job for processing outbox messages.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class ProcessOutboxMessagesJob : IJob
{
    private static readonly JsonSerializerSettings JsonSerializerSettings = new()
    {
        TypeNameHandling = TypeNameHandling.All
    };

    private readonly ISqlQueryExecutor _sqlQueryExecutor;
    private readonly IPublisher _publisher;
    private readonly ISystemTime _systemTime;
    private readonly ProcessOutboxMessagesOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessOutboxMessagesJob"/> class.
    /// </summary>
    /// <param name="sqlQueryExecutor">The SQL query executor.</param>
    /// <param name="publisher">The publisher.</param>
    /// <param name="systemTime">The system time.</param>
    /// <param name="options">The options.</param>
    public ProcessOutboxMessagesJob(
        ISqlQueryExecutor sqlQueryExecutor,
        IPublisher publisher,
        ISystemTime systemTime,
        IOptions<ProcessOutboxMessagesOptions> options)
    {
        _sqlQueryExecutor = sqlQueryExecutor;
        _publisher = publisher;
        _systemTime = systemTime;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        string getOutboxMessagesSql = $@"
            SELECT id, content
            FROM users.outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc
            LIMIT {_options.BatchSize}";

        IEnumerable<OutboxMessageResponse> outboxMessages = await _sqlQueryExecutor.QueryAsync<OutboxMessageResponse>(getOutboxMessagesSql);

        var outboxMessagesList = outboxMessages.ToList();

        if (!outboxMessagesList.Any())
        {
            return;
        }

        AsyncRetryPolicy policy = CreateRetryPolicy();

        const string updateOutboxMessageSql = @"
            UPDATE users.outbox_messages
            SET processed_on_utc = @ProcessedOnUtc,
                error = @Error
            WHERE id = @Id";

        foreach (OutboxMessageResponse outboxMessage in outboxMessagesList)
        {
            IDomainEvent domainEvent = JsonConvert.DeserializeObject<IDomainEvent>(outboxMessage.Content, JsonSerializerSettings)!;

            PolicyResult result = await policy.ExecuteAndCaptureAsync(() => _publisher.Publish(domainEvent, context.CancellationToken));

            await _sqlQueryExecutor.ExecuteAsync(
                updateOutboxMessageSql,
                new
                {
                    outboxMessage.Id,
                    ProcessedOnUtc = _systemTime.UtcNow,
                    Error = result.FinalException?.ToString()
                });
        }
    }

    private AsyncRetryPolicy CreateRetryPolicy() =>
        Policy
            .Handle<Exception>()
            .RetryAsync(_options.RetryCount);

    internal sealed record OutboxMessageResponse(Guid Id, string Content);
}
