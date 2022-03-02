using Application.Data;
using Application.Time;
using Domain.Primitives;
using MediatR;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessOutboxMessagesJob"/> class.
    /// </summary>
    /// <param name="sqlQueryExecutor">The SQL query executor.</param>
    /// <param name="publisher">The publisher.</param>
    /// <param name="systemTime">The system time.</param>
    public ProcessOutboxMessagesJob(ISqlQueryExecutor sqlQueryExecutor, IPublisher publisher, ISystemTime systemTime)
    {
        _sqlQueryExecutor = sqlQueryExecutor;
        _publisher = publisher;
        _systemTime = systemTime;
    }

    /// <inheritdoc />
    public async Task Execute(IJobExecutionContext context)
    {
        const string getOutboxMessagesSql = @"
            SELECT id, content
            FROM users.outbox_messages
            WHERE processed_on_utc IS NULL
            ORDER BY occurred_on_utc";

        IEnumerable<OutboxMessageResponse> outboxMessages = await _sqlQueryExecutor.QueryAsync<OutboxMessageResponse>(getOutboxMessagesSql);

        var outboxMessagesList = outboxMessages.ToList();

        if (!outboxMessagesList.Any())
        {
            return;
        }

        AsyncRetryPolicy policy = Policy
            .Handle<Exception>()
            .RetryAsync(3);

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

    internal sealed record OutboxMessageResponse(Guid Id, string Content);
}
