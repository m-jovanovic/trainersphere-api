using Modules.Users.Domain;

namespace Modules.Users.Persistence;

/// <summary>
/// Represents the unit of work.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default) => throw new NotImplementedException();
}
