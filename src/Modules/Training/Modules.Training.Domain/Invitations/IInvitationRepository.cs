using Shared.Results;

namespace Modules.Training.Domain.Invitations;

/// <summary>
/// Represents the invitation repository interface.
/// </summary>
public interface IInvitationRepository
{
    /// <summary>
    /// Checks if there is a pending invitation for the specified email.
    /// </summary>
    /// <param name="email">The email.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The success result if there is no pending invitation, otherwise a failure result.</returns>
    Task<Result> CheckIfPendingExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified invitation to the repository.
    /// </summary>
    /// <param name="invitation">The invitation.</param>
    void Add(Invitation invitation);
}
