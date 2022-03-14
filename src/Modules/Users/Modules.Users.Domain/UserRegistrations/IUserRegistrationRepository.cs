using Shared.Results;

namespace Modules.Users.Domain.UserRegistrations;

/// <summary>
/// Represents the user registration repository interface.
/// </summary>
public interface IUserRegistrationRepository
{
    Task<Result> CheckNonePendingForEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds the specified user registration to the repository.
    /// </summary>
    /// <param name="userRegistration">The user registration.</param>
    void Add(UserRegistration userRegistration);
}
