using Application.Contracts.EventBus;

namespace Modules.Training.IntegrationEvents;

/// <summary>
/// Represents the invitation sent integration event.
/// </summary>
/// <param name="Id">The identifier.</param>
/// <param name="OccurredOnUtc">The occurred on date and time.</param>
/// <param name="InvitationId">The invitation identifier.</param>
/// <param name="TrainerId">The trainer identifier.</param>
/// <param name="Email">The email.</param>
public sealed record InvitationSentIntegrationEvent(
    Guid Id,
    DateTime OccurredOnUtc,
    Guid InvitationId,
    Guid TrainerId,
    string Email) : IntegrationEvent(Id, OccurredOnUtc);
