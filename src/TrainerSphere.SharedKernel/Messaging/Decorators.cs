using System.Diagnostics;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Extensions.Logging;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Outbox;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.SharedKernel.Messaging;

internal static partial class HandlerLog
{
    [LoggerMessage(Level = LogLevel.Information, Message = "{Request} failed with {ErrorCode} in {ElapsedMs} ms")]
    public static partial void Failed(ILogger logger, string request, string errorCode, long elapsedMs);

    [LoggerMessage(Level = LogLevel.Debug, Message = "{Request} succeeded in {ElapsedMs} ms")]
    public static partial void Succeeded(ILogger logger, string request, long elapsedMs);
}

internal static class ValidationRunner
{
    public static async Task<ValidationError?> Validate<T>(IEnumerable<IValidator<T>> validators, T request, CancellationToken cancellationToken)
    {
        List<ValidationFailure> failures = [];

        foreach (IValidator<T> validator in validators)
        {
            FluentValidation.Results.ValidationResult result = await validator.ValidateAsync(request, cancellationToken);
            failures.AddRange(result.Errors);
        }

        return failures.Count == 0
            ? null
            : new ValidationError(failures.Select(f => new FieldError(FieldNames.ToJsonPath(f.PropertyName), f.ErrorCode, f.ErrorMessage)));
    }
}

internal sealed class LoggingCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    ILogger<LoggingCommandDecorator<TCommand, TResponse>> logger) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        Result<TResponse> result = await inner.Handle(command, cancellationToken);
        long elapsed = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (result.TryGetError(out Error? error))
        {
            HandlerLog.Failed(logger, typeof(TCommand).FullName ?? typeof(TCommand).Name, error.Code, elapsed);
        }
        else
        {
            HandlerLog.Succeeded(logger, typeof(TCommand).FullName ?? typeof(TCommand).Name, elapsed);
        }

        return result;
    }
}

internal sealed class LoggingQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    ILogger<LoggingQueryDecorator<TQuery, TResponse>> logger) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        long start = Stopwatch.GetTimestamp();
        Result<TResponse> result = await inner.Handle(query, cancellationToken);
        long elapsed = (long)Stopwatch.GetElapsedTime(start).TotalMilliseconds;

        if (result.TryGetError(out Error? error))
        {
            HandlerLog.Failed(logger, typeof(TQuery).FullName ?? typeof(TQuery).Name, error.Code, elapsed);
        }
        else
        {
            HandlerLog.Succeeded(logger, typeof(TQuery).FullName ?? typeof(TQuery).Name, elapsed);
        }

        return result;
    }
}

internal sealed class ValidationCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IEnumerable<IValidator<TCommand>> validators) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        ValidationError? error = await ValidationRunner.Validate(validators, command, cancellationToken);

        return error is null ? await inner.Handle(command, cancellationToken) : Result.Failure<TResponse>(error);
    }
}

internal sealed class ValidationQueryDecorator<TQuery, TResponse>(
    IQueryHandler<TQuery, TResponse> inner,
    IEnumerable<IValidator<TQuery>> validators) : IQueryHandler<TQuery, TResponse>
    where TQuery : IQuery<TResponse>
    where TResponse : notnull
{
    public async Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken)
    {
        ValidationError? error = await ValidationRunner.Validate(validators, query, cancellationToken);

        return error is null ? await inner.Handle(query, cancellationToken) : Result.Failure<TResponse>(error);
    }
}

/// <summary>
/// Runs the command in a transaction; on success writes collected domain events to the outbox and commits,
/// on failure (or exception) rolls back so a failed result never leaves partial writes behind.
/// </summary>
internal sealed class TransactionalCommandDecorator<TCommand, TResponse>(
    ICommandHandler<TCommand, TResponse> inner,
    IDbSession session,
    IDomainEventCollector collector) : ICommandHandler<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
    where TResponse : notnull
{
    public async Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken)
    {
        if (session.HasTransaction)
        {
            return await inner.Handle(command, cancellationToken);
        }

        await session.BeginTransactionAsync(cancellationToken);

        try
        {
            Result<TResponse> result = await inner.Handle(command, cancellationToken);

            if (result.IsSuccess)
            {
                await collector.FlushToOutboxAsync(session, cancellationToken);
                await session.CommitAsync(cancellationToken);
            }
            else
            {
                collector.Clear();
                await session.RollbackAsync(cancellationToken);
            }

            return result;
        }
        catch
        {
            collector.Clear();
            await session.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
