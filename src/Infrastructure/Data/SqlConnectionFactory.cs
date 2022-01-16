using System.Data;
using Application.Data;
using Npgsql;

namespace Infrastructure.Data
{
    /// <summary>
    /// Represents the SQL connection factory.
    /// </summary>
    internal sealed class SqlConnectionFactory : ISqlConnectionFactory, IDisposable
    {
        private readonly string _connectionString;
        private IDbConnection? _connection;

        public SqlConnectionFactory(string connectionString) => _connectionString = connectionString;

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
}
