using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Users.Domain.Users;
using Modules.Users.Persistence.Constants;

namespace Modules.Users.Persistence.Configurations;

/// <summary>
/// Represents the <see cref="User"/> entity configuration.
/// </summary>
internal sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable(TableNames.Users);

        builder.HasKey(user => user.Id);

        builder.Property(user => user.Email).IsRequired().HasMaxLength(300);

        builder.HasIndex(user => user.Email).IsUnique();

        builder.Property(user => user.FirstName).IsRequired().HasMaxLength(100);

        builder.Property(user => user.LastName).IsRequired().HasMaxLength(100);
    }
}
