using Domain.Results;
using Modules.Users.Domain.Users;
using Shared.ServiceLifetimes;

namespace Modules.Users.Infrastructure.IdentityProvider;

/// <summary>
/// Represents the identity provider service.
/// </summary>
internal sealed class IdentityProviderService : IIdentityProviderService, ITransient
{
    public Task<Result> ExistsAsync(string identityProviderId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Result.Failure(UserErrors.IdentityNotFound));
}
