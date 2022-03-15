using Application.Contracts.EventBus;

namespace Modules.Users.IntegrationEvents;

/// <summary>
/// Represents the client user registered integration event.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="OccurredOnUtc">The occurred on date and time.</param>
/// <param name="UserRegistrationId">The user registration identifier.</param>
/// <param name="UserId">The user identifier.</param>
/// <param name="Email">The email.</param>
/// <param name="FirstName">The first name.</param>
/// <param name="LastName">The last name.</param>
public sealed record ClientUserRegisteredIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid UserRegistrationId,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName) : IntegrationEvent(Id, OccurredOnUtc);
