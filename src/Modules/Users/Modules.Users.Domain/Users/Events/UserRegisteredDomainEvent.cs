using Domain.Primitives;

namespace Modules.Users.Domain.Users.Events;

/// <summary>
/// Represents the event that is raised then a new user is registered.
/// </summary>
/// <param name="UserId">The user identifier.</param>
/// <param name="Email">The email.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record UserRegisteredDomainEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    UserId UserId,
    string Email,
    string FirstName,
    string LastName) : DomainEvent(Id, OccurredOnUtc);
