using Microsoft.AspNetCore.Mvc;

namespace Modules.Training.Endpoints.Contracts.Invitations;

/// <summary>
/// Represents the request for inviting a client.
/// </summary>
public sealed class InviteClientRequest
{
    /// <summary>
    /// Gets the coach identifier.
    /// </summary>
    [FromRoute(Name = "coachId")]
    public Guid CoachId { get; init; }

    /// <summary>
    /// Gets the client email.
    /// </summary>
    [FromBody]
    public string Email { get; init; } = string.Empty;
}
