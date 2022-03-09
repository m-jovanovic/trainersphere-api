namespace Modules.Training.Persistence.Constants;

/// <summary>
/// Represents the table names in the training module.
/// </summary>
internal static class TableNames
{
    /// <summary>
    /// The outbox messages table.
    /// </summary>
    internal const string OutboxMessages = "outbox_messages";

    /// <summary>
    /// The outbox message consumers table.
    /// </summary>
    internal const string OutboxMessageConsumers = "outbox_message_consumers";
}
