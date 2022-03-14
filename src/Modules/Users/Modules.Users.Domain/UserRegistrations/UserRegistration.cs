using Domain.Primitives;

namespace Modules.Users.Domain.UserRegistrations;

/// <summary>
/// Represents the user registration entity.
/// </summary>
public sealed class UserRegistration : Entity<UserRegistrationId>, IAuditable
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserRegistration"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="email">The email.</param>
    /// <param name="status">The status.</param>
    private UserRegistration(UserRegistrationId id, string email, UserRegistrationStatus status)
        : base(id)
    {
        Email = email;
        Status = status;
    }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// Gets the status.
    /// </summary>
    public UserRegistrationStatus Status { get; private set; }

    /// <inheritdoc />
    public DateTime CreatedOnUtc { get; private set; }

    /// <inheritdoc />
    public DateTime? ModifiedOnUtc { get; private set; }

    /// <summary>
    /// Creates a new user registration with the specified parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="email">The email.</param>
    /// <returns>The new user registration instance.</returns>
    public static UserRegistration Create(UserRegistrationId id, string email)
    {
        var userRegistration = new UserRegistration(id, email, UserRegistrationStatus.Pending);

        return userRegistration;
    }
}
