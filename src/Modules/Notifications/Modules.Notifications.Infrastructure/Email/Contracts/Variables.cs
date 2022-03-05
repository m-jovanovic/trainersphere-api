namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the variables.
/// </summary>
/// <param name="Email">The email.</param>
/// <param name="Substitutions">The substitutions.</param>
internal sealed record Variables(Recipient Email, Substitution[] Substitutions);
