using Application.Data;
using Application.Messaging;
using Domain.Results;
using Modules.Users.Application.Boundary.Users.Queries.GetUserByIdentityProviderId;
using Modules.Users.Domain.Users;

namespace Modules.Users.Application.Users.Queries.GetUserByIdentityProviderId;

/// <summary>
/// Represents the <see cref="GetUserByIdentityProviderIdQuery"/> handler.
/// </summary>
internal sealed class GetUserByIdentityProviderIdQueryHandler : IQueryHandler<GetUserByIdentityProviderIdQuery, UserResponse>
{
    private readonly ISqlQueryExecutor _sqlQueryExecutor;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserByIdentityProviderIdQueryHandler"/> class.
    /// </summary>
    /// <param name="sqlQueryExecutor">The SQL connection factory.</param>
    public GetUserByIdentityProviderIdQueryHandler(ISqlQueryExecutor sqlQueryExecutor) => _sqlQueryExecutor = sqlQueryExecutor;

    /// <inheritdoc />
    public async Task<Result<UserResponse>> Handle(GetUserByIdentityProviderIdQuery request, CancellationToken cancellationToken) =>
        await Result.Success(request)
            .Bind(async query => Result.Create(await GetUserByIdentityProviderId(query)))
            .MapFailure(() => UserErrors.NotFoundByIdentity(request.IdentityProviderId));

    private async Task<UserResponse?> GetUserByIdentityProviderId(GetUserByIdentityProviderIdQuery query) =>
        await _sqlQueryExecutor.FirstOrDefaultAsync<UserResponse>(
                @"SELECT id, email, first_name, last_name
                  FROM users.users
                  WHERE identity_provider_id = @IdentityProviderId",
                new { query.IdentityProviderId });
}
