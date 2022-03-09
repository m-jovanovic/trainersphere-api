using Domain.Primitives;

namespace Modules.Users.Domain.Roles;

/// <summary>
/// Represents the permission enumeration.
/// </summary>
public sealed class Permission : Enumeration<Permission>
{
    public static readonly Permission UserRead = new(100, nameof(UserRead), "Can read user information.");
    public static readonly Permission UserModify = new(101, nameof(UserModify), "Can modify user information.");

    /// <summary>
    /// Initializes a new instance of the <see cref="Permission"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="name">The name.</param>
    /// <param name="description">The description.</param>
    private Permission(int id, string name, string description)
        : base(id, name) =>
        Description = description;

    /// <summary>
    /// Initializes a new instance of the <see cref="Permission"/> class.
    /// </summary>
    private Permission() => Description = string.Empty;

    /// <summary>
    /// Gets the description.
    /// </summary>
    public string Description { get; private init; }
}
