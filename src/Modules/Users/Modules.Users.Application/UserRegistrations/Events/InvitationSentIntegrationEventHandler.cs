using Application.EventBus;
using Modules.Training.IntegrationEvents;
using Modules.Users.Domain;
using Modules.Users.Domain.UserRegistrations;
using Shared.Results;

namespace Modules.Users.Application.UserRegistrations.Events;

/// <summary>
/// Represents the <see cref="InvitationSentIntegrationEvent"/> handler.
/// </summary>
internal sealed class InvitationSentIntegrationEventHandler : IntegrationEventHandler<InvitationSentIntegrationEvent>
{
    private readonly IUserRegistrationRepository _userRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;

    /// <summary>
    /// Initializes a new instance of the <see cref="InvitationSentIntegrationEventHandler"/> class.
    /// </summary>
    /// <param name="userRegistrationRepository">The user registration repository.</param>
    /// <param name="unitOfWork">The unit of work.</param>
    public InvitationSentIntegrationEventHandler(IUserRegistrationRepository userRegistrationRepository, IUnitOfWork unitOfWork)
    {
        _userRegistrationRepository = userRegistrationRepository;
        _unitOfWork = unitOfWork;
    }

    /// <inheritdoc />
    public override async Task Handle(InvitationSentIntegrationEvent integrationEvent, CancellationToken cancellationToken = default) =>
        await UserRegistration.Create(
            new UserRegistrationId(integrationEvent.Id),
            integrationEvent.Email)
            .Bind(userRegistration => CheckNoPendingInvitationsAsync(userRegistration, cancellationToken))
            .Tap(userRegistration => _userRegistrationRepository.Add(userRegistration))
            .Tap(() => _unitOfWork.SaveChangesAsync(cancellationToken));

    private async Task<Result<UserRegistration>> CheckNoPendingInvitationsAsync(UserRegistration invitation, CancellationToken cancellationToken) =>
        await _userRegistrationRepository.CheckNonePendingForEmailAsync(invitation.Email, cancellationToken)
            .Map(() => invitation)
            .MapFailure(() => UserRegistrationErrors.PendingExists);
}
