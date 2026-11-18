using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.Api.Modules.Notifications.Data;

internal sealed class RecipientRepository(IDbSession db)
{
    public Task<Recipient?> GetAsync(Guid userId, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<Recipient>(
            "SELECT user_id, email, first_name, last_name, role, trainer_id FROM notifications.recipients WHERE user_id = @UserId",
            new { UserId = userId },
            cancellationToken);

    public Task UpsertAsync(Recipient recipient, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO notifications.recipients (user_id, email, first_name, last_name, role, trainer_id, updated_at)
            VALUES (@UserId, @Email, @FirstName, @LastName, @Role, @TrainerId, @Now)
            ON CONFLICT (user_id) DO UPDATE
            SET email = excluded.email, first_name = excluded.first_name, last_name = excluded.last_name,
                role = excluded.role, trainer_id = excluded.trainer_id, updated_at = excluded.updated_at
            """,
            new { recipient.UserId, recipient.Email, recipient.FirstName, recipient.LastName, recipient.Role, recipient.TrainerId, Now = now },
            cancellationToken);

    public Task UpdateNameAsync(Guid userId, string firstName, string lastName, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "UPDATE notifications.recipients SET first_name = @FirstName, last_name = @LastName, updated_at = @Now WHERE user_id = @UserId",
            new { UserId = userId, FirstName = firstName, LastName = lastName, Now = now },
            cancellationToken);
}
