using Microsoft.Extensions.Options;
using Persistence.Data;

namespace App.ServiceInstallers.Database;

/// <summary>
/// Represents the <see cref="ConnectionString"/> setup.
/// </summary>
public class ConnectionStringSetup : IConfigureOptions<ConnectionString>
{
    private const string ConnectionStringName = "Database";
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionStringSetup"/> class.
    /// </summary>
    /// <param name="configuration">The configuration.</param>
    public ConnectionStringSetup(IConfiguration configuration) => _configuration = configuration;

    /// <inheritdoc />
    public void Configure(ConnectionString options) => options.Value = _configuration.GetConnectionString(ConnectionStringName);
}
