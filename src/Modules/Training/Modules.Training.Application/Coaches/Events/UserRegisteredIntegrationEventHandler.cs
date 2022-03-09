using Application.EventBus;
using Modules.Training.Domain;
using Modules.Training.Domain.Coaches;
using Modules.Users.IntegrationEvents;
using Shared.Results;

namespace Modules.Training.Application.Coaches.Events;

/// <summary>
/// Represents the <see cref="UserRegisteredIntegrationEvent"/> handler.
/// </summary>
internal sealed class UserRegisteredIntegrationEventHandler : IIntegrationEventHandler<UserRegisteredIntegrationEvent>
{
    private readonly ICoachRepository _coachRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegisteredIntegrationEventHandler"/> class.
    /// </summary>
    /// <param name="coachRepository">The coach repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public UserRegisteredIntegrationEventHandler(ICoachRepository coachRepository, IUnitOfWork unitOfWork)
    {
        _coachRepository = coachRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public async Task Handle(UserRegisteredIntegrationEvent notification, CancellationToken cancellationToken) =>
        await Result.Create(notification)
            .FlatMap(userRegisteredIntegrationEvent =>
                Coach.Create(
                    new CoachId(userRegisteredIntegrationEvent.UserId),
                    notification.Email,
                    notification.FirstName,
                    notification.LastName))
            .Tap(coach => _coachRepository.Add(coach))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken));
}
