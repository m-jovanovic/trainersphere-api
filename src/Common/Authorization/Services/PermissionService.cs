using System.Globalization;
using Authorization.Contracts;
using MassTransit;
using Microsoft.Extensions.Caching.Memory;

namespace Authorization.Services;

/// <summary>
/// Represents the permission service.
/// </summary>
internal sealed class PermissionService : IPermissionService
{
    private const string CacheKeyFormat = "permissions-{0}";
    private readonly IRequestClient<IUserPermissionsRequest> _userPermissionsRequestClient;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionService"/> class.
    /// </summary>
    /// <param name="userPermissionsRequestClient">The user permissions request client.</param>
    /// <param name="memoryCache">The memory cache.</param>
    public PermissionService(IRequestClient<IUserPermissionsRequest> userPermissionsRequestClient, IMemoryCache memoryCache)
    {
        _userPermissionsRequestClient = userPermissionsRequestClient;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<HashSet<string>> GetPermissionsAsync(string identityProviderId, CancellationToken cancellationToken = default) =>
        await _memoryCache.GetOrCreateAsync(
            CreateCacheKey(identityProviderId),
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                return await GetPermissions(identityProviderId, cancellationToken);
            });

    private static string CreateCacheKey(string identityProviderId) =>
        string.Format(CultureInfo.InvariantCulture, CacheKeyFormat, identityProviderId);

    private async Task<HashSet<string>> GetPermissions(string identityProviderId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(identityProviderId))
        {
            return new HashSet<string>();
        }

        var request = new UserPermissionsRequest
        {
            UserIdentityProviderId = identityProviderId
        };

        Response<IUserPermissionsResponse> response = await _userPermissionsRequestClient
            .GetResponse<IUserPermissionsResponse>(request, cancellationToken);

        return response.Message.Permissions;
    }

    private sealed class UserPermissionsRequest : IUserPermissionsRequest
    {
        public string UserIdentityProviderId { get; init; } = string.Empty;
    }
}
