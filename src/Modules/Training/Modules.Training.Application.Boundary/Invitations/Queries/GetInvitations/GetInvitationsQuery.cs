using Application.Contracts.Messaging;

namespace Modules.Training.Application.Boundary.Invitations.Queries.GetInvitations;

/// <summary>
/// Represents the query for getting a list of invitations.
/// </summary>
/// <param name="TrainerId">The trainer identifier.</param>
public sealed record GetInvitationsQuery(Guid TrainerId) : IQuery<IReadOnlyList<InvitationResponse>>;
