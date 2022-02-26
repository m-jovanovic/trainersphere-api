using Application.Messaging;

namespace Modules.Users.Application.Boundary.Users.Queries.GetUserByIdentityProviderId;

/// <summary>
/// Represents the query for getting a user by identity provider identifier.
/// </summary>
/// <param name="IdentityProviderId">The identity provider identifier.</param>
public sealed record GetUserByIdentityProviderIdQuery(string IdentityProviderId) : IQuery<UserResponse>;
