using Application.EventBus;
using Modules.Training.Domain;
using Modules.Training.Domain.Trainers;
using Modules.Users.IntegrationEvents;
using Shared.Results;

namespace Modules.Training.Application.Trainers.Events;

/// <summary>
/// Represents the <see cref="UserRegisteredIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegisteredIntegrationEventHandler"/> class.
    /// </summary>
    /// <param name="trainerRepository">The trainer repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public UserRegisteredIntegrationEventHandler(ITrainerRepository trainerRepository, IUnitOfWork unitOfWork)
    {
        _trainerRepository = trainerRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task Handle(UserRegisteredIntegrationEvent integrationEvent, CancellationToken cancellationToken) =>
        await Result.Create(integrationEvent)
            .Bind(userRegisteredIntegrationEvent =>
                Trainer.Create(
                    new TrainerId(userRegisteredIntegrationEvent.UserId),
                    integrationEvent.Email,
                    integrationEvent.FirstName,
                    integrationEvent.LastName))
            .Tap(trainer => _trainerRepository.Add(trainer))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken));
}
