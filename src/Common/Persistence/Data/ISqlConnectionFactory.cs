using System.Data;

namespace Persistence.Data;

/// <summary>
/// Represents the SQL connection factory interface.
/// </summary>
public interface ISqlConnectionFactory
{
    /// <summary>
    /// Gets the open database connection instance.
    /// </summary>
    /// <returns>The open database connection instance.</returns>
    IDbConnection GetOpenConnection();
}
