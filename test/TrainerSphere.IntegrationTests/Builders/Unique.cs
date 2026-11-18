namespace TrainerSphere.IntegrationTests.Builders;

/// <summary>
/// Unique values so tests sharing a fixture database never collide.
/// </summary>
public static class Unique
{
    private static int _counter;

    public static int Next() => Interlocked.Increment(ref _counter);

    public static string Email(string prefix = "user") => $"{prefix}-{Guid.NewGuid():N}@test.trainersphere.dev";

    public static string Name(string prefix) => $"{prefix} {Next()}";
}
