using Microsoft.EntityFrameworkCore;

namespace Modules.Users.Persistence;

/// <summary>
/// Represents the users module database context.
/// </summary>
internal sealed class UsersDbContext : DbContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UsersDbContext"/> class.
    /// </summary>
    /// <param name="options">The database context options.</param>
    public UsersDbContext(DbContextOptions options)
        : base(options)
    {
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("users");

        modelBuilder.ApplyConfigurationsFromAssembly(AssemblyReference.Assembly);
    }
}
