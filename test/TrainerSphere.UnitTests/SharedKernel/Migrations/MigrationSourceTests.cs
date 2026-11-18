using Shouldly;
using TrainerSphere.SharedKernel.Migrations;

namespace TrainerSphere.UnitTests.SharedKernel.Migrations;

public sealed class MigrationSourceTests
{
    private const string FixturePrefix = "TrainerSphere.UnitTests.MigrationFixtures";

    [Fact]
    public void Load_WithTwoMigrationsOfTheSameVersion_Throws()
    {
        MigrationSource source = new("fixture", typeof(MigrationSourceTests).Assembly, $"{FixturePrefix}.Duplicate");

        InvalidOperationException error = Should.Throw<InvalidOperationException>(source.Load);

        error.Message.ShouldContain("fixture");
        error.Message.ShouldContain("version 1");
    }

    [Fact]
    public void Load_OrdersByVersionRegardlessOfFileName()
    {
        MigrationSource source = new("fixture", typeof(MigrationSourceTests).Assembly, $"{FixturePrefix}.Ordered");

        IReadOnlyList<Migration> migrations = source.Load();

        migrations.Select(m => m.Version).ShouldBe([1, 2]);
        migrations[0].Name.ShouldBe("zulu");
        migrations[1].Name.ShouldBe("alpha");
    }

    [Fact]
    public void Load_WithUnparseableFileName_Throws()
    {
        MigrationSource source = new("fixture", typeof(MigrationSourceTests).Assembly, $"{FixturePrefix}.Misnamed");

        Should.Throw<InvalidOperationException>(source.Load).Message.ShouldContain("V0000__name.sql");
    }
}
