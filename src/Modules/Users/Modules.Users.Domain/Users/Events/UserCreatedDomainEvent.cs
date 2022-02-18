using Domain.Primitives;

namespace Modules.Users.Domain.Users.Events
{
    public sealed record UserCreatedDomainEvent(Guid Id, string Email, string FirstName, string LastName) : IDomainEvent;
}
