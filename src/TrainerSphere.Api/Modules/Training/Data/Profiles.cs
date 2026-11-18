using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Http;

namespace TrainerSphere.Api.Modules.Training.Data;

internal interface IClientRepository
{
    Task<ClientProfile?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientProfile>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task UpdateAsync(ClientProfile client, CancellationToken cancellationToken);

    Task<string?> GetTrainerTimeZoneAsync(Guid trainerId, CancellationToken cancellationToken);
}

/// <summary>
/// Maintains the trainer and client projections from Identity events. Every write is idempotent so redelivered
/// events are harmless, and profile updates older than the stored one are ignored.
/// </summary>
internal interface IProfileProjection
{
    Task UpsertTrainerAsync(TrainerRegistered registered, CancellationToken cancellationToken);

    Task UpsertClientAsync(ClientRegistered registered, CancellationToken cancellationToken);

    Task ApplyProfileUpdateAsync(UserProfileUpdated updated, CancellationToken cancellationToken);
}

internal sealed class ClientRepository(IDbSession db) : IClientRepository
{
    public const string Columns = "id, trainer_id, email, first_name, last_name, time_zone, status, joined_at, archived_at, goals, trainer_notes, updated_at";

    public async Task<ClientProfile?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ClientRecord? record = await db.QuerySingleOrDefaultAsync<ClientRecord>(
            $"SELECT {Columns} FROM training.clients WHERE id = @Id",
            new { Id = id },
            cancellationToken);

        return record is null ? null : ClientProfile.Restore(record);
    }

    public async Task<IReadOnlyList<ClientProfile>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<ClientRecord> records = await db.QueryAsync<ClientRecord>(
            $"SELECT {Columns} FROM training.clients WHERE id = ANY(@Ids)",
            new { Ids = ids.ToArray() },
            cancellationToken);

        return [.. records.Select(ClientProfile.Restore)];
    }

    public Task UpdateAsync(ClientProfile client, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            UPDATE training.clients
            SET status = @Status, archived_at = @ArchivedAt, goals = @Goals, trainer_notes = @TrainerNotes, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new { client.Id, Status = client.Status.ToString(), client.ArchivedAt, client.Goals, client.TrainerNotes, client.UpdatedAt },
            cancellationToken);

    public Task<string?> GetTrainerTimeZoneAsync(Guid trainerId, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<string>("SELECT time_zone FROM training.trainers WHERE id = @Id", new { Id = trainerId }, cancellationToken);
}

internal sealed class ProfileProjection(IDbSession db) : IProfileProjection
{
    public Task UpsertTrainerAsync(TrainerRegistered registered, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.trainers (id, email, first_name, last_name, time_zone, units, registered_at, profile_updated_at)
            VALUES (@UserId, @Email, @FirstName, @LastName, @TimeZone, 'Kg', @OccurredAt, @OccurredAt)
            ON CONFLICT (id) DO NOTHING
            """,
            new { registered.UserId, registered.Email, registered.FirstName, registered.LastName, registered.TimeZone, registered.OccurredAt },
            cancellationToken);

    public Task UpsertClientAsync(ClientRegistered registered, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.clients (id, trainer_id, email, first_name, last_name, time_zone, units, status, joined_at, profile_updated_at, updated_at)
            VALUES (@UserId, @TrainerId, @Email, @FirstName, @LastName, @TimeZone, 'Kg', 'Active', @OccurredAt, @OccurredAt, @OccurredAt)
            ON CONFLICT (id) DO NOTHING
            """,
            new { registered.UserId, registered.TrainerId, registered.Email, registered.FirstName, registered.LastName, registered.TimeZone, registered.OccurredAt },
            cancellationToken);

    public async Task ApplyProfileUpdateAsync(UserProfileUpdated updated, CancellationToken cancellationToken)
    {
        string units = (KebabCase.TryParse(updated.Units, out Units parsed) ? parsed : Units.Kg).ToString();
        var parameters = new { updated.UserId, updated.FirstName, updated.LastName, Units = units, updated.TimeZone, updated.OccurredAt };

        await db.ExecuteAsync(
            """
            UPDATE training.trainers
            SET first_name = @FirstName, last_name = @LastName, units = @Units, time_zone = @TimeZone, profile_updated_at = @OccurredAt
            WHERE id = @UserId AND profile_updated_at <= @OccurredAt
            """,
            parameters,
            cancellationToken);
        await db.ExecuteAsync(
            """
            UPDATE training.clients
            SET first_name = @FirstName, last_name = @LastName, units = @Units, time_zone = @TimeZone, profile_updated_at = @OccurredAt
            WHERE id = @UserId AND profile_updated_at <= @OccurredAt
            """,
            parameters,
            cancellationToken);
    }
}
