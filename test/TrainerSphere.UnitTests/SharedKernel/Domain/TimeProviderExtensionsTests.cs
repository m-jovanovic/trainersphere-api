using Microsoft.Extensions.Time.Testing;
using Shouldly;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.UnitTests.SharedKernel.Domain;

public sealed class TimeProviderExtensionsTests
{
    [Fact]
    public void UtcNow_TruncatesToWholeMicroseconds()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 11, 18, 10, 15, 3, TimeSpan.Zero).AddTicks(8_284_684));

        DateTime now = clock.UtcNow();

        now.Kind.ShouldBe(DateTimeKind.Utc);
        now.ShouldBe(new DateTime(2026, 11, 18, 10, 15, 3, DateTimeKind.Utc).AddTicks(8_284_680));
        now.ToString("O", System.Globalization.CultureInfo.InvariantCulture).ShouldBe("2026-11-18T10:15:03.8284680Z");
    }

    [Fact]
    public void UtcNow_KeepsValuesThatAreAlreadyWholeMicroseconds()
    {
        DateTimeOffset exact = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1_234_560);

        new FakeTimeProvider(exact).UtcNow().ShouldBe(exact.UtcDateTime);
    }

    [Fact]
    public void UtcNow_ConvertsNonUtcOffsetsToUtc()
    {
        FakeTimeProvider clock = new(new DateTimeOffset(2026, 3, 1, 12, 0, 0, TimeSpan.FromHours(2)));

        clock.UtcNow().ShouldBe(new DateTime(2026, 3, 1, 10, 0, 0, DateTimeKind.Utc));
    }
}
