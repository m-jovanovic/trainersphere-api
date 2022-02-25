using Application.Data;
using Dapper;
using Shared.ServiceLifetimes;

namespace Persistence.Data;

/// <summary>
/// Represents the SQL query executor.
/// </summary>
internal sealed class SqlQueryExecutor : ISqlQueryExecutor, ITransient
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="SqlQueryExecutor"/> class.
    /// </summary>
    /// <param name="sqlConnectionFactory">The SQL connection factory.</param>
    public SqlQueryExecutor(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

    /// <inheritdoc />
    public async Task<T?> FirstOrDefaultAsync<T>(string sql, object parameters) =>
        await _sqlConnectionFactory.GetOpenConnection().QueryFirstOrDefaultAsync<T>(sql, parameters);
}
