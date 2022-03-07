using Authorization.Contracts;
using MassTransit;

namespace Authorization.Services;

/// <summary>
/// Represents the permission service.
/// </summary>
internal sealed class PermissionService : IPermissionService
{
    private readonly IRequestClient<IUserPermissionsRequest> _userPermissionsRequestClient;

    public PermissionService(IRequestClient<IUserPermissionsRequest> userPermissionsRequestClient) =>
        _userPermissionsRequestClient = userPermissionsRequestClient;

    /// <inheritdoc />
    public async Task<HashSet<string>> GetPermissionsAsync(string identityProviderId, CancellationToken cancellationToken = default)
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
