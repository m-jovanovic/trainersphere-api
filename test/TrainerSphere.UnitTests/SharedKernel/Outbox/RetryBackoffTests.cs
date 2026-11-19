using Shouldly;
using TrainerSphere.SharedKernel.Outbox;

namespace TrainerSphere.UnitTests.SharedKernel.Outbox;

public sealed class RetryBackoffTests
{
    private static readonly MessagingOptions Defaults = new();

    [Theory]
    [InlineData(1, 1_000)]
    [InlineData(2, 5_000)]
    [InlineData(3, 25_000)]
    [InlineData(4, 125_000)]
    [InlineData(5, 600_000)]
    [InlineData(50, 600_000)]
    public void DelayAfter_GrowsExponentiallyUpToTheCap(int failedAttempts, int expectedMilliseconds) =>
        RetryBackoff.DelayAfter(failedAttempts, Defaults, jitter: 0.5).ShouldBe(TimeSpan.FromMilliseconds(expectedMilliseconds));

    [Theory]
    [InlineData(0.0, 800)]
    [InlineData(0.999_999, 1_200)]
    public void DelayAfter_JittersByAtMostTwentyPercent(double jitter, int expectedMilliseconds) =>
        RetryBackoff.DelayAfter(1, Defaults, jitter).TotalMilliseconds.ShouldBe(expectedMilliseconds, tolerance: 0.01);

    [Fact]
    public void DelayAfter_WithRandomJitter_StaysWithinBounds()
    {
        for (int i = 0; i < 1_000; i++)
        {
            RetryBackoff.DelayAfter(2, Defaults).TotalMilliseconds.ShouldBeInRange(4_000, 6_000);
        }
    }

    [Fact]
    public void DelayAfter_RejectsZeroAttempts() =>
        Should.Throw<ArgumentOutOfRangeException>(() => RetryBackoff.DelayAfter(0, Defaults, 0.5));
}
