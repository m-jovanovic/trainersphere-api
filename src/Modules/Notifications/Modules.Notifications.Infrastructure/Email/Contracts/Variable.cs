namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the variable.
/// </summary>
/// <param name="Email">The email.</param>
/// <param name="Substitutions">The substitutions.</param>
internal sealed record Variable(string Email, Substitution[] Substitutions);
