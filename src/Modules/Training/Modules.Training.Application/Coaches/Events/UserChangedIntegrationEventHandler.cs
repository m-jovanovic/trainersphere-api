using Application.EventBus;
using Microsoft.Extensions.Logging;
using Modules.Training.Domain;
using Modules.Training.Domain.Coaches;
using Modules.Users.IntegrationEvents;
using Shared.Results;

namespace Modules.Training.Application.Coaches.Events;

/// <summary>
/// Represents the <see cref="UserChangedIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserChangedIntegrationEventHandler : IIntegrationEventHandler<UserChangedIntegrationEvent>
{
    private readonly ICoachRepository _coachRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UserChangedIntegrationEvent> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserChangedIntegrationEventHandler"/> class.
    /// </summary>
    /// <param name="coachRepository">The coach repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    /// <param name="logger">The logger.</param>
    public UserChangedIntegrationEventHandler(
        ICoachRepository coachRepository,
        IUnitOfWork unitOfWork,
        ILogger<UserChangedIntegrationEvent> logger)
    {
        _coachRepository = coachRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task Handle(UserChangedIntegrationEvent notification, CancellationToken cancellationToken) =>
        await _coachRepository.GetByIdAsync(new CoachId(notification.UserId), cancellationToken)
            .Tap(coach => coach.Change(notification.FirstName, notification.LastName))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken))
            .OnFailure(error => _logger.LogError("Error while processing message: {@MessageId}, {@Error}", notification.Id, error));
}
