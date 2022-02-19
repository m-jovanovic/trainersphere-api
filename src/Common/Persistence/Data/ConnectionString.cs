namespace Persistence.Data;

/// <summary>
/// Represents the connection string.
/// </summary>
public sealed class ConnectionString
{
    /// <summary>
    /// Gets or sets the connection string value.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    public static implicit operator string(ConnectionString connectionString) => connectionString.Value;
}
