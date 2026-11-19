using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Identity.Contracts;

/// <summary>A trainer created an account.</summary>
internal sealed record TrainerRegistered(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string FirstName,
    string LastName,
    string TimeZone) : IntegrationEvent(Id, OccurredAt);

/// <summary>A client accepted an invitation and now has an account linked to the trainer.</summary>
internal sealed record ClientRegistered(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    Guid TrainerId,
    Guid InvitationId,
    string Email,
    string FirstName,
    string LastName,
    string TimeZone) : IntegrationEvent(Id, OccurredAt);

/// <summary>A user changed their name, units or time zone.</summary>
internal sealed record UserProfileUpdated(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string FirstName,
    string LastName,
    string Units,
    string TimeZone) : IntegrationEvent(Id, OccurredAt);

/// <summary>
/// An invitation link was created or re-sent. Carries the raw token because only the email can deliver it;
/// the token is never stored in the Identity schema. <c>CreatedAt</c> is the invitation's own creation time, which a
/// re-send keeps; <c>OccurredAt</c> is when this link was issued.
/// </summary>
internal sealed record InvitationIssued(
    Guid Id,
    DateTime OccurredAt,
    Guid InvitationId,
    Guid TrainerId,
    string Email,
    string FirstName,
    string LastName,
    string TrainerName,
    string? Message,
    string Token,
    DateTime CreatedAt,
    DateTime ExpiresAt) : IntegrationEvent(Id, OccurredAt);

/// <summary>A pending invitation was cancelled.</summary>
internal sealed record InvitationCancelled(Guid Id, DateTime OccurredAt, Guid InvitationId, Guid TrainerId) : IntegrationEvent(Id, OccurredAt);

/// <summary>A user asked for a password reset link. Carries the raw token for the email.</summary>
internal sealed record PasswordResetRequested(
    Guid Id,
    DateTime OccurredAt,
    Guid UserId,
    string Email,
    string FirstName,
    string Token,
    DateTime ExpiresAt) : IntegrationEvent(Id, OccurredAt);
