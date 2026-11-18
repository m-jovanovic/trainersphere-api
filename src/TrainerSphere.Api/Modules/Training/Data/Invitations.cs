using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.Api.Modules.Training.Data;

/// <summary>
/// Maintains the trainer's pending-invitation projection from Identity events, so the dashboard never reads the
/// Identity schema. Every write is idempotent: a re-sent invitation only extends the expiry (its creation time and
/// list position stay those of GET /api/invitations), accepts and cancels remove it.
/// </summary>
internal interface IInvitationProjection
{
    Task TrackAsync(InvitationIssued issued, CancellationToken cancellationToken);

    Task RemoveAsync(Guid invitationId, CancellationToken cancellationToken);

    Task<(int Count, IReadOnlyList<PendingInvitation> Items)> GetPendingAsync(Guid trainerId, DateTime now, int limit, CancellationToken cancellationToken);
}

internal sealed class InvitationProjection(IDbSession db) : IInvitationProjection
{
    private const string Columns = "id, email, first_name, last_name, message, created_at, expires_at";

    public Task TrackAsync(InvitationIssued issued, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.invitations (id, trainer_id, email, first_name, last_name, message, created_at, expires_at)
            VALUES (@Id, @TrainerId, @Email, @FirstName, @LastName, @Message, @CreatedAt, @ExpiresAt)
            ON CONFLICT (id) DO UPDATE SET expires_at = GREATEST(training.invitations.expires_at, EXCLUDED.expires_at)
            """,
            new
            {
                Id = issued.InvitationId,
                issued.TrainerId,
                issued.Email,
                issued.FirstName,
                issued.LastName,
                issued.Message,
                issued.CreatedAt,
                issued.ExpiresAt,
            },
            cancellationToken);

    public Task RemoveAsync(Guid invitationId, CancellationToken cancellationToken) =>
        db.ExecuteAsync("DELETE FROM training.invitations WHERE id = @Id", new { Id = invitationId }, cancellationToken);

    public async Task<(int Count, IReadOnlyList<PendingInvitation> Items)> GetPendingAsync(Guid trainerId, DateTime now, int limit, CancellationToken cancellationToken)
    {
        const string Filter = "trainer_id = @TrainerId AND expires_at > @Now";
        var parameters = new { TrainerId = trainerId, Now = now, Limit = limit };

        int count = await db.QuerySingleAsync<int>($"SELECT count(*)::int FROM training.invitations WHERE {Filter}", parameters, cancellationToken);
        IReadOnlyList<PendingInvitation> items = await db.QueryAsync<PendingInvitation>(
            $"""
            SELECT {Columns} FROM training.invitations
            WHERE {Filter}
            ORDER BY created_at DESC, id DESC
            LIMIT @Limit
            """,
            parameters,
            cancellationToken);

        return (count, items);
    }
}

internal sealed record PendingInvitation
{
    public Guid Id { get; init; }

    public string Email { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public string? Message { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }
}
