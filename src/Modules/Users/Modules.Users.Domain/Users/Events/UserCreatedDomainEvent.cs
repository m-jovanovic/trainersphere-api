using Domain.Primitives;

namespace Modules.Users.Domain.Users.Events;

/// <summary>
/// Represents the event that is raised then a new user is created.
/// </summary>
/// <param name="UserId">The user identifier.</param>
public sealed record UserCreatedDomainEvent(UserId UserId) : DomainEvent;
