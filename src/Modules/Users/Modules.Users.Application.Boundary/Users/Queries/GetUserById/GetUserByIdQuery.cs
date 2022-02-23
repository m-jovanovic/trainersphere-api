using Application.Messaging;

namespace Modules.Users.Application.Boundary.Users.Queries.GetUserById
{
    public sealed record GetUserByIdQuery(Guid UserId) : IQuery<UserResponse>;
}
