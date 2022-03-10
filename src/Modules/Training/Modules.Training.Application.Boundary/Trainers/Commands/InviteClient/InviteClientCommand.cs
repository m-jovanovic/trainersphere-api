using Application.Contracts.Messaging;

namespace Modules.Training.Application.Boundary.Trainers.Commands.InviteClient;

/// <summary>
/// Invites the client with the specified email to train with the trainer with the specified identifier.
/// </summary>
/// <param name="TrainerId">The trainer identifier.</param>
/// <param name="Email">The client email.</param>
public sealed record InviteClientCommand(Guid TrainerId, string Email) : ICommand<Guid>;
