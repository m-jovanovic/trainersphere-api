namespace TrainerSphere.SharedKernel.Domain;

public static class Ids
{
    public static Guid New(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);

        return Guid.CreateVersion7(timeProvider.GetUtcNow());
    }
}
