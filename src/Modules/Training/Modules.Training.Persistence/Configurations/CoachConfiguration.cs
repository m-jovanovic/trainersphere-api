using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Modules.Training.Domain.Coaches;
using Modules.Training.Persistence.Constants;
using Shared.Extensions;

namespace Modules.Training.Persistence.Configurations;

/// <summary>
/// Represents the <see cref="Coach"/> entity configuration.
/// </summary>
internal sealed class CoachConfiguration : IEntityTypeConfiguration<Coach>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Coach> builder) =>
        builder
            .Tap(ConfigureDataStructure)
            .Tap(ConfigureIndexes);

    private static void ConfigureDataStructure(EntityTypeBuilder<Coach> builder)
    {
        builder.ToTable(TableNames.Coaches);

        builder.HasKey(coach => coach.Id);

        builder.Property(user => user.Id).ValueGeneratedNever().HasConversion(coachId => coachId.Value, value => new CoachId(value));

        builder.Property(user => user.Email).IsRequired().HasMaxLength(300);

        builder.Property(user => user.FirstName).IsRequired().HasMaxLength(100);

        builder.Property(user => user.LastName).IsRequired().HasMaxLength(100);

        builder.Property(user => user.CreatedOnUtc).IsRequired();

        builder.Property(user => user.ModifiedOnUtc).IsRequired(false);
    }

    private static void ConfigureIndexes(EntityTypeBuilder<Coach> builder) =>
        builder.HasIndex(user => user.Email).IsUnique();
}
