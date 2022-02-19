using Domain.Primitives;
using Domain.Results;
using Modules.Users.Domain.Users.Events;

namespace Modules.Users.Domain.Users;

/// <summary>
/// Represents the user entity.
/// </summary>
public sealed class User : Entity
{
    /// <summary>
    /// Initializes a new instance of the <see cref="User"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="email">The email.</param>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    private User(Guid id, string email, string firstName, string lastName)
        : base(id)
    {
        Email = email;
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; private set; }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string FirstName { get; private set; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public string LastName { get; private set; }

    /// <summary>
    /// Creates a new user with the specified parameters.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="email">The email.</param>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    /// <returns>The new user instance.</returns>
    public static Result<User> Create(Guid id, string email, string firstName, string lastName)
    {
        var user = new User(id, email, firstName, lastName);

        user.RaiseDomainEvent(new UserCreatedDomainEvent(user.Id, user.Email, user.FirstName, user.LastName));

        return user;
    }
}
