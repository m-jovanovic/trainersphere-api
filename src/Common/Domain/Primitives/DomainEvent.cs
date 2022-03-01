using Domain.Time;

namespace Domain.Primitives;

/// <summary>
/// Represents the abstract domain event primitive.
/// </summary>
public abstract record DomainEvent : IDomainEvent
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DomainEvent"/> class.
    /// </summary>
    protected DomainEvent()
    {
        Id = Guid.NewGuid();
        OccurredOnUtc = SystemTimeProvider.UtcNow();
    }

    /// <inheritdoc />
    public Guid Id { get; }

    /// <inheritdoc />
    public DateTime OccurredOnUtc { get; }
}
