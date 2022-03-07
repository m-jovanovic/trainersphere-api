using Authorization.Contracts;
using Authorization.Extensions;
using Authorization.Requirements;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;

namespace Authorization.AuthorizationHandlers;

/// <summary>
/// Represents the permission authorization handler.
/// </summary>
internal sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="PermissionAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The service scope factory.</param>
    public PermissionAuthorizationHandler(IServiceScopeFactory serviceScopeFactory) => _serviceScopeFactory = serviceScopeFactory;

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();

        IRequestClient<IUserPermissionsRequest> userRolesRequestClient = scope.ServiceProvider.GetService<IRequestClient<IUserPermissionsRequest>>()!;

        var request = new UserPermissionsRequest
        {
            UserIdentityProviderId = context.User.GetIdentityProviderId()
        };

        Response<IUserPermissionsResponse> response = await userRolesRequestClient.GetResponse<IUserPermissionsResponse>(request);

        if (response.Message.Permissions.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }
    }

    private sealed class UserPermissionsRequest : IUserPermissionsRequest
    {
        public string UserIdentityProviderId { get; init; } = string.Empty;
    }
}
