using Application.Contracts.EventBus;

namespace Modules.Training.IntegrationEvents;

/// <summary>
/// Represents the invitation created integration event.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="OccurredOnUtc">The occurred on date and time.</param>
/// <param name="InvitationId">The invitation identifier.</param>
/// <param name="Email">The email.</param>
public sealed record InvitationCreatedIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid InvitationId,
    string Email) : IntegrationEvent(Id, OccurredOnUtc);
