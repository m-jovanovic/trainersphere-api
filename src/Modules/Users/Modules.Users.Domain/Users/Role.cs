using Domain.Primitives;

namespace Modules.Users.Domain.Users;

/// <summary>
/// Represents the role enumeration.
/// </summary>
public sealed class Role : Enumeration<Role>
{
    public static readonly Role Administrator = new(1, "Administrator");
    public static readonly Role Registered = new(2, "Registered");
    public static readonly Role Coach = new(3, "Coach");
    public static readonly Role Client = new(4, "Client");

    /// <summary>
    /// Initializes a new instance of the <see cref="Role"/> class.
    /// </summary>
    /// <param name="id">The identifier.</param>
    /// <param name="name">The name.</param>
    private Role(int id, string name)
        : base(id, name)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="Role"/> class.
    /// </summary>
    private Role()
    {
    }
}
