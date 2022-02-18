using Domain.Primitives;

namespace Modules.Users.Domain.Users.Events;

/// <summary>
/// Represents the event that is raised then a new user is created.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="Email">The email.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record UserCreatedDomainEvent(Guid Id, string Email, string FirstName, string LastName) : IDomainEvent;
