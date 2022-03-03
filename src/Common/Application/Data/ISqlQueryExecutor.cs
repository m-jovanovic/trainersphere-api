namespace Application.Data;

/// <summary>
/// Represents the SQL query executor.
/// </summary>
public interface ISqlQueryExecutor
{
    /// <summary>
    /// Executes the provided SQL query with the provided parameters object and returns the values found.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">The parameters object.</param>
    /// <returns>The values found.</returns>
    Task<IEnumerable<T>> QueryAsync<T>(string sql, object? parameters = default);

    /// <summary>
    /// Executes the provided SQL query with the provided parameters object and returns the first value found or the default value.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">The parameters object.</param>
    /// <returns>The first value found or the default value.</returns>
    Task<T?> FirstOrDefaultAsync<T>(string sql, object? parameters = default);

    /// <summary>
    /// Executes the provided SQL query with the provided parameters object.
    /// </summary>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">The parameters object.</param>
    /// <returns>The completed task.</returns>
    Task ExecuteAsync(string sql, object? parameters = default);

    /// <summary>
    /// Executes the provided SQL query with the provided parameters object.
    /// </summary>
    /// <typeparam name="T">The result type.</typeparam>
    /// <param name="sql">The SQL query.</param>
    /// <param name="parameters">The parameters object.</param>
    /// <returns>The completed task.</returns>
    Task<T> ExecuteScalarAsync<T>(string sql, object? parameters = default);
}
