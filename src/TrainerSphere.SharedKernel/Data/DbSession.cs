using System.Data;
using Dapper;
using Npgsql;

namespace TrainerSphere.SharedKernel.Data;

/// <summary>
/// A per-scope database connection with an optional transaction. All Dapper calls go through it so they
/// automatically join the current transaction.
/// </summary>
public interface IDbSession
{
    bool HasTransaction { get; }

    Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken);

    Task BeginTransactionAsync(CancellationToken cancellationToken);

    Task CommitAsync(CancellationToken cancellationToken);

    Task RollbackAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Runs the action after the current transaction commits, or immediately when there is none.
    /// </summary>
    void OnCommitted(Action action);

    Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken cancellationToken);

    Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken cancellationToken);

    Task<T> QuerySingleAsync<T>(string sql, object? param, CancellationToken cancellationToken);

    Task<T?> ExecuteScalarAsync<T>(string sql, object? param, CancellationToken cancellationToken);

    Task<int> ExecuteAsync(string sql, object? param, CancellationToken cancellationToken);

    Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param, CancellationToken cancellationToken);
}

internal sealed class DbSession(NpgsqlDataSource dataSource) : IDbSession, IAsyncDisposable
{
    private readonly List<Action> _afterCommit = [];
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _transaction;

    public bool HasTransaction => _transaction is not null;

    public async Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is null)
        {
            _connection = await dataSource.OpenConnectionAsync(cancellationToken);
        }
        else if (_connection.State != ConnectionState.Open)
        {
            await _connection.OpenAsync(cancellationToken);
        }

        return _connection;
    }

    public async Task BeginTransactionAsync(CancellationToken cancellationToken)
    {
        if (_transaction is not null)
        {
            throw new InvalidOperationException("A transaction is already in progress.");
        }

        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        _transaction = await connection.BeginTransactionAsync(cancellationToken);
    }

    public async Task CommitAsync(CancellationToken cancellationToken)
    {
        if (_transaction is null)
        {
            throw new InvalidOperationException("No transaction is in progress.");
        }

        await _transaction.CommitAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;

        Action[] callbacks = [.. _afterCommit];
        _afterCommit.Clear();

        foreach (Action callback in callbacks)
        {
            callback();
        }
    }

    public async Task RollbackAsync(CancellationToken cancellationToken)
    {
        _afterCommit.Clear();

        if (_transaction is null)
        {
            return;
        }

        await _transaction.RollbackAsync(cancellationToken);
        await _transaction.DisposeAsync();
        _transaction = null;
    }

    public void OnCommitted(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);

        if (_transaction is null)
        {
            action();
        }
        else
        {
            _afterCommit.Add(action);
        }
    }

    public async Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        IEnumerable<T> rows = await connection.QueryAsync<T>(Command(sql, param, cancellationToken));
        return rows.AsList();
    }

    public async Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<T>(Command(sql, param, cancellationToken));
    }

    public async Task<T> QuerySingleAsync<T>(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        return await connection.QuerySingleAsync<T>(Command(sql, param, cancellationToken));
    }

    public async Task<T?> ExecuteScalarAsync<T>(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<T>(Command(sql, param, cancellationToken));
    }

    public async Task<int> ExecuteAsync(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        return await connection.ExecuteAsync(Command(sql, param, cancellationToken));
    }

    public async Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param, CancellationToken cancellationToken)
    {
        NpgsqlConnection connection = await GetConnectionAsync(cancellationToken);
        return await connection.QueryMultipleAsync(Command(sql, param, cancellationToken));
    }

    public async ValueTask DisposeAsync()
    {
        if (_transaction is not null)
        {
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            _connection = null;
        }
    }

    private CommandDefinition Command(string sql, object? param, CancellationToken cancellationToken) =>
        new(sql, param, _transaction, cancellationToken: cancellationToken);
}
