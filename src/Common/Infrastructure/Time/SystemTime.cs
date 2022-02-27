using Application.Time;
using Shared.ServiceLifetimes;

namespace Infrastructure.Time;

/// <summary>
/// Represents the system time interface.
/// </summary>
public sealed class SystemTime : ISystemTime, ITransient
{
    /// <inheritdoc />
    public DateTime UtcNow => DateTime.UtcNow;
}
