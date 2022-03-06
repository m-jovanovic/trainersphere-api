namespace Modules.Users.Persistence.Constants;

/// <summary>
/// Represents the table names in the users module.
/// </summary>
internal static class TableNames
{
    /// <summary>
    /// The users table.
    /// </summary>
    internal const string Users = "users";

    /// <summary>
    /// The user roles table.
    /// </summary>
    internal const string UserRoles = "user_roles";

    /// <summary>
    /// The roles table.
    /// </summary>
    internal const string Roles = "roles";

    /// <summary>
    /// The roles table.
    /// </summary>
    internal const string Permissions = "permissions";

    /// <summary>
    /// The outbox messages table.
    /// </summary>
    internal const string OutboxMessages = "outbox_messages";

    /// <summary>
    /// The outbox message consumers table.
    /// </summary>
    internal const string OutboxMessageConsumers = "outbox_message_consumers";
}
