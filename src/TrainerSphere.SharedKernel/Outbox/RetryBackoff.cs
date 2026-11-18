using System.Diagnostics.CodeAnalysis;

namespace TrainerSphere.SharedKernel.Outbox;

public static class RetryBackoff
{
    private const double JitterRatio = 0.2;

    /// <summary>
    /// Returns how long to wait after the given number of failed attempts: base, base x multiplier, base x
    /// multiplier^2 ... capped at the maximum, with +/-20% jitter so messages that failed together do not retry in lockstep.
    /// </summary>
    /// <param name="failedAttempts">The number of failed attempts so far, at least 1.</param>
    /// <param name="options">The messaging options.</param>
    /// <returns>The delay.</returns>
    [SuppressMessage("Security", "CA5394:Do not use insecure randomness", Justification = "Retry jitter is not security-sensitive.")]
    public static TimeSpan DelayAfter(int failedAttempts, MessagingOptions options) =>
        DelayAfter(failedAttempts, options, Random.Shared.NextDouble());

    /// <inheritdoc cref="DelayAfter(int, MessagingOptions)"/>
    /// <param name="failedAttempts">The number of failed attempts so far, at least 1.</param>
    /// <param name="options">The messaging options.</param>
    /// <param name="jitter">A value in [0, 1); 0.5 means no jitter.</param>
    public static TimeSpan DelayAfter(int failedAttempts, MessagingOptions options, double jitter)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentOutOfRangeException.ThrowIfLessThan(failedAttempts, 1);

        double delay = Math.Min(
            options.RetryBaseDelayMilliseconds * Math.Pow(options.RetryBackoffMultiplier, failedAttempts - 1),
            options.RetryMaxDelayMilliseconds);

        return TimeSpan.FromMilliseconds(delay * (1 - JitterRatio + (2 * JitterRatio * jitter)));
    }
}
