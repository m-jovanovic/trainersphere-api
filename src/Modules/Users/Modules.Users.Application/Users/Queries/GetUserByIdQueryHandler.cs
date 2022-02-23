using Application.Data;
using Application.Messaging;
using Dapper;
using Domain.Results;
using Modules.Users.Application.Boundary.Users.Queries.GetUserById;
using Modules.Users.Domain.Users;

namespace Modules.Users.Application.Users.Queries;

/// <summary>
/// Represents the <see cref="GetUserByIdQuery"/> handler.
/// </summary>
internal sealed class GetUserByIdQueryHandler : IQueryHandler<GetUserByIdQuery, UserResponse>
{
    private readonly ISqlConnectionFactory _sqlConnectionFactory;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetUserByIdQueryHandler"/> class.
    /// </summary>
    /// <param name="sqlConnectionFactory">The SQL connection factory.</param>
    public GetUserByIdQueryHandler(ISqlConnectionFactory sqlConnectionFactory) => _sqlConnectionFactory = sqlConnectionFactory;

    /// <inheritdoc />
    public async Task<Result<UserResponse>> Handle(GetUserByIdQuery request, CancellationToken cancellationToken) =>
        await Result.Success(request)
            .Bind(async query => Result.Create(await GetUserById(query)))
            .MapFailure(() => UserErrors.NotFound(request.UserId));

    private async Task<UserResponse?> GetUserById(GetUserByIdQuery query) =>
        await _sqlConnectionFactory.GetOpenConnection()
            .QueryFirstOrDefaultAsync<UserResponse>(
                @"SELECT * FROM users.""Users"" WHERE ""Id"" = @UserId",
                new { query.UserId });
}
