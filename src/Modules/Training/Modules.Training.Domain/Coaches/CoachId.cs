using Domain.Primitives;

namespace Modules.Training.Domain.Coaches;

/// <summary>
/// Represents the coach identifier.
/// </summary>
/// <param name="Value">The value.</param>
public sealed record CoachId(Guid Value) : IEntityId;
