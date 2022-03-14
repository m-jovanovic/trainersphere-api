using Domain.Primitives;

namespace Modules.Training.Domain.Invitations;

/// <summary>
/// Represents the sender value object.
/// </summary>
public sealed class Sender : ValueObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="Sender"/> class.
    /// </summary>
    /// <param name="firstName">The first name.</param>
    /// <param name="lastName">The last name.</param>
    public Sender(string firstName, string lastName)
    {
        FirstName = firstName;
        LastName = lastName;
    }

    /// <summary>
    /// Gets the first name.
    /// </summary>
    public string FirstName { get; }

    /// <summary>
    /// Gets the last name.
    /// </summary>
    public string LastName { get; }

    /// <inheritdoc />
    protected override IEnumerable<object> GetAtomicValues()
    {
        yield return FirstName;
        yield return LastName;
    }
}
