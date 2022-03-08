using Application.Data;
using Microsoft.Extensions.Caching.Memory;

namespace Modules.Users.Infrastructure.Authorization.SameUser;

/// <summary>
/// Represents the user checker.
/// </summary>
internal sealed class UserChecker : IUserChecker
{
    private readonly ISqlQueryExecutor _sqlQueryExecutor;
    private readonly IMemoryCache _memoryCache;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserChecker"/> class.
    /// </summary>
    /// <param name="sqlQueryExecutor">The SQL query executor.</param>
    /// <param name="memoryCache">The memory cache.</param>
    public UserChecker(ISqlQueryExecutor sqlQueryExecutor, IMemoryCache memoryCache)
    {
        _sqlQueryExecutor = sqlQueryExecutor;
        _memoryCache = memoryCache;
    }

    /// <inheritdoc />
    public async Task<bool> ExistsAsync(Guid userId, string identityProviderId, CancellationToken cancellationToken = default) =>
        await _memoryCache.GetOrCreateAsync(
            CreateCacheKey(userId, identityProviderId),
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);

                return await ExistsInternalAsync(userId, identityProviderId);
            });

    private static string CreateCacheKey(Guid userId, string identityProviderId) => $"users-{userId}-{identityProviderId}";

    private async Task<bool> ExistsInternalAsync(Guid userId, string identityProviderId)
    {
        const string sql = @"
            SELECT EXISTS(
                SELECT 1
                FROM users.users
                WHERE id = @UserId AND
                      identity_provider_id = @IdentityProviderId
            )";

        return await _sqlQueryExecutor.ExecuteScalarAsync<bool>(
            sql,
            new
            {
                UserId = userId,
                IdentityProviderId = identityProviderId
            });
    }
}
