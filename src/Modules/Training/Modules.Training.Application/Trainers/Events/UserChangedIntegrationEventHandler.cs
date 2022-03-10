using Application.EventBus;
using Microsoft.Extensions.Logging;
using Modules.Training.Domain;
using Modules.Training.Domain.Trainers;
using Modules.Users.IntegrationEvents;
using Shared.Results;

namespace Modules.Training.Application.Trainers.Events;

/// <summary>
/// Represents the <see cref="UserChangedIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserChangedIntegrationEventHandler : IIntegrationEventHandler<UserChangedIntegrationEvent>
{
    private readonly ITrainerRepository _trainerRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserChangedIntegrationEvent> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserChangedIntegrationEventHandler"/> class.
    /// </summary>
    /// <param name="trainerRepository">The trainer repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="logger">The logger.</param>
    public UserChangedIntegrationEventHandler(
        ITrainerRepository trainerRepository,
        IUnitOfWork unitOfWork,
        ILogger<UserChangedIntegrationEvent> logger)
    {
        _trainerRepository = trainerRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(UserChangedIntegrationEvent notification, CancellationToken cancellationToken) =>
        await _trainerRepository.GetByIdAsync(new TrainerId(notification.UserId), cancellationToken)
            .Tap(trainer => trainer.Change(notification.FirstName, notification.LastName))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken))
            .OnFailure(error => _logger.LogError("Error while processing message: {@MessageId}, {@Error}", notification.Id, error));
}
