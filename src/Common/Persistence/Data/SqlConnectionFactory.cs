using System.Data;
using Microsoft.Extensions.Options;
using Npgsql;
using Shared.ServiceLifetimes;

namespace Persistence.Data;

/// <summary>
/// Represents the SQL connection factory.
/// </summary>
internal sealed class SqlConnectionFactory : ISqlConnectionFactory, IDisposable, ITransient
{
    private readonly ConnectionString _connectionString;
    private IDbConnection? _connection;

    public SqlConnectionFactory(IOptions<ConnectionString> connectionString) => _connectionString = connectionString.Value;

    /// <inheritdoc />
    public IDbConnection GetOpenConnection()
    {
        if ((_connection ??= new NpgsqlConnection(_connectionString)).State != ConnectionState.Open)
        {
            _connection.Open();
        }

        return _connection;
    }

    /// <inheritdoc />
    public void Dispose() => _connection?.Dispose();
}
