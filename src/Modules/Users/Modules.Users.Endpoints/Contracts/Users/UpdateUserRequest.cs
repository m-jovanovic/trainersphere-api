using Microsoft.AspNetCore.Mvc;

namespace Modules.Users.Endpoints.Contracts.Users;

/// <summary>
/// Represents the request for updating a user's basic information.
/// </summary>
public sealed class UpdateUserRequest
{
    /// <summary>
    /// Gets the user identifier.
    /// </summary>
    [FromRoute(Name = "userId")]
    public Guid UserId { get; init; }

    /// <summary>
    /// Gets the user details.
    /// </summary>
    [FromBody]
    public UpdateUserDetailsRequest UserDetails { get; init; } = new(string.Empty, string.Empty);
}
