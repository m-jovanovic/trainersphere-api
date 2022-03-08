using Endpoints.Extensions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Modules.Users.Application.Boundary.Users.Queries.GetUserByIdentityProviderId;
using Shared.Results;

namespace Modules.Users.Infrastructure.Authorization.SameUser;

/// <summary>
/// Represents the same user policy authorization handler.
/// </summary>
internal sealed class SameUserAuthorizationHandler : AuthorizationHandler<SameUserRequirement>
{
    private const string UserIdResourceName = "userId";
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;

    /// <summary>
    /// Initializes a new instance of the <see cref="SameUserAuthorizationHandler"/> class.
    /// </summary>
    /// <param name="serviceScopeFactory">The service scope factory.</param>
    /// <param name="httpContextAccessor">The HTTP context accessor.</param>
    public SameUserAuthorizationHandler(IServiceScopeFactory serviceScopeFactory, IHttpContextAccessor httpContextAccessor)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _httpContextAccessor = httpContextAccessor;
    }

    /// <inheritdoc />
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, SameUserRequirement requirement)
    {
        string? identityProviderId = _httpContextAccessor.HttpContext?.User.GetIdentityProviderId();

        if (string.IsNullOrWhiteSpace(identityProviderId))
        {
            return;
        }

        Guid? userIdFromRoute = GetUserIdFromRoute();

        if (!userIdFromRoute.HasValue)
        {
            return;
        }

        Result<UserResponse> userResult = await GetUserAsync(identityProviderId);

        if (userResult.IsSuccess && userResult.Value.Id == userIdFromRoute)
        {
            context.Succeed(requirement);
        }
    }

    private async Task<Result<UserResponse>> GetUserAsync(string identityProviderId)
    {
        using IServiceScope scope = _serviceScopeFactory.CreateScope();

        ISender sender = scope.ServiceProvider.GetService<ISender>()!;

        var query = new GetUserByIdentityProviderIdQuery(identityProviderId);

        return await sender.Send(query);
    }

    private Guid? GetUserIdFromRoute()
    {
        RouteValueDictionary? routeValueDictionary = _httpContextAccessor.HttpContext?.GetRouteData().Values;

        if (routeValueDictionary is null)
        {
            return null;
        }

        return routeValueDictionary.TryGetValue(UserIdResourceName, out object? userIdValue) &&
               Guid.TryParse(userIdValue?.ToString(), out Guid userId)
            ? userId
            : null;
    }
}
