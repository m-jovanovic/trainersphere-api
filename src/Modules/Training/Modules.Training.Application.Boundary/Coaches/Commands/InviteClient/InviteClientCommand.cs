using Application.Contracts.Messaging;

namespace Modules.Training.Application.Boundary.Coaches.Commands.InviteClient;

/// <summary>
/// Invites the client with the specified email to train with the coach with the specified identifier.
/// </summary>
/// <param name="CoachId">The coach identifier.</param>
/// <param name="Email">The client email.</param>
public sealed record InviteClientCommand(Guid CoachId, string Email) : ICommand<Guid>;
