using Shared.Extensions;

namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the variable.
/// </summary>
internal sealed record Variable
{
    private Variable()
    {
    }

    /// <summary>
    /// Gets the email.
    /// </summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>
    /// Gets the substitutions.
    /// </summary>
    public Substitution[] Substitutions { get; init; } = Array.Empty<Substitution>();

    public static Variable Create() => new();

    public Variable WithEmail(string email) => this with
    {
        Email = email
    };

    public Variable WithSubstitution(string var, string value) => this with
    {
        Substitutions = new List<Substitution> { new(var, value) }
            .Tap(list => list.AddRange(Substitutions))
            .ToArray()
    };
}
