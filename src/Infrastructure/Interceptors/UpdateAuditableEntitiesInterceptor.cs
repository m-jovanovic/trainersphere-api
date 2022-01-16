using Application.Time;
using Domain.Primitives;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Infrastructure.Interceptors
{
    /// <summary>
    /// Represents the interceptor for updating auditable entity values.
    /// </summary>
    internal sealed class UpdateAuditableEntitiesInterceptor : SaveChangesInterceptor
    {
        private readonly ISystemTime _systemTime;

        /// <summary>
        /// Initializes a new instance of the <see cref="UpdateAuditableEntitiesInterceptor"/> class.
        /// </summary>
        /// <param name="systemTime">The system time.</param>
        public UpdateAuditableEntitiesInterceptor(ISystemTime systemTime) => _systemTime = systemTime;

        /// <inheritdoc />
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            IEnumerable<EntityEntry<IAuditable>> auditableEntities =
                eventData.Context?.ChangeTracker.Entries<IAuditable>() ?? Array.Empty<EntityEntry<IAuditable>>();

            DateTime utcNow = _systemTime.UtcNow;
            foreach (EntityEntry<IAuditable> auditable in auditableEntities)
            {
                if (auditable.State == EntityState.Added)
                {
                    auditable.Property(nameof(IAuditable.CreatedOnUtc)).CurrentValue = utcNow;
                }

                if (auditable.State == EntityState.Modified)
                {
                    auditable.Property(nameof(IAuditable.ModifiedOnUtc)).CurrentValue = utcNow;
                }
            }

            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }
    }
}
