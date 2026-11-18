using Npgsql;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.Api.Modules.Identity.Data;

internal sealed class UserRepository(IDbSession db)
{
    private const string Columns = "id, email, password_hash, first_name, last_name, role, trainer_id, units, time_zone, created_at, updated_at";

    public async Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        UserRecord? record = await db.QuerySingleOrDefaultAsync<UserRecord>(
            $"SELECT {Columns} FROM identity.users WHERE id = @Id",
            new { Id = id },
            cancellationToken);

        return record is null ? null : User.Restore(record);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken)
    {
        UserRecord? record = await db.QuerySingleOrDefaultAsync<UserRecord>(
            $"SELECT {Columns} FROM identity.users WHERE email = @Email",
            new { Email = email },
            cancellationToken);

        return record is null ? null : User.Restore(record);
    }

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.QuerySingleAsync<bool>("SELECT EXISTS (SELECT 1 FROM identity.users WHERE email = @Email)", new { Email = email }, cancellationToken);

    /// <summary>
    /// Inserts the user; returns false when the email is taken (a concurrent signup won the unique index).
    /// </summary>
    public async Task<bool> TryAddAsync(User user, CancellationToken cancellationToken)
    {
        try
        {
            await db.ExecuteAsync(
                """
                INSERT INTO identity.users (id, email, password_hash, first_name, last_name, role, trainer_id, units, time_zone, created_at, updated_at)
                VALUES (@Id, @Email, @PasswordHash, @FirstName, @LastName, @Role, @TrainerId, @Units, @TimeZone, @CreatedAt, @UpdatedAt)
                """,
                new
                {
                    user.Id,
                    user.Email,
                    user.PasswordHash,
                    user.FirstName,
                    user.LastName,
                    Role = user.Role.ToString(),
                    user.TrainerId,
                    Units = user.Units.ToString(),
                    user.TimeZone,
                    user.CreatedAt,
                    user.UpdatedAt,
                },
                cancellationToken);

            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            UPDATE identity.users
            SET password_hash = @PasswordHash, first_name = @FirstName, last_name = @LastName, units = @Units, time_zone = @TimeZone, updated_at = @UpdatedAt
            WHERE id = @Id
            """,
            new { user.Id, user.PasswordHash, user.FirstName, user.LastName, Units = user.Units.ToString(), user.TimeZone, user.UpdatedAt },
            cancellationToken);
}

internal sealed record RefreshTokenRecord
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public Guid FamilyId { get; init; }

    public string TokenHash { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? RevokedAt { get; init; }

    public Guid? ReplacedById { get; init; }
}

internal sealed class RefreshTokenRepository(IDbSession db, NpgsqlDataSource dataSource)
{
    public Task AddAsync(RefreshTokenRecord token, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO identity.refresh_tokens (id, user_id, family_id, token_hash, created_at, expires_at)
            VALUES (@Id, @UserId, @FamilyId, @TokenHash, @CreatedAt, @ExpiresAt)
            """,
            token,
            cancellationToken);

    public Task<RefreshTokenRecord?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<RefreshTokenRecord>(
            """
            SELECT id, user_id, family_id, token_hash, created_at, expires_at, revoked_at, replaced_by_id
            FROM identity.refresh_tokens
            WHERE token_hash = @TokenHash
            """,
            new { TokenHash = tokenHash },
            cancellationToken);

    /// <summary>
    /// Marks the token as rotated. Returns false when it was already revoked, i.e. a concurrent refresh used it first.
    /// </summary>
    public async Task<bool> TryRotateAsync(Guid id, Guid replacedById, DateTime now, CancellationToken cancellationToken) =>
        await db.ExecuteAsync(
            "UPDATE identity.refresh_tokens SET revoked_at = @Now, replaced_by_id = @ReplacedById WHERE id = @Id AND revoked_at IS NULL",
            new { Id = id, ReplacedById = replacedById, Now = now },
            cancellationToken) == 1;

    public Task RevokeAsync(Guid id, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "UPDATE identity.refresh_tokens SET revoked_at = @Now WHERE id = @Id AND revoked_at IS NULL",
            new { Id = id, Now = now },
            cancellationToken);

    public Task RevokeAllForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "UPDATE identity.refresh_tokens SET revoked_at = @Now WHERE user_id = @UserId AND revoked_at IS NULL",
            new { UserId = userId, Now = now },
            cancellationToken);

    /// <summary>
    /// Revokes every token of the family on its own connection, so the revocation commits even though the
    /// surrounding command fails (and rolls back) with 401.
    /// </summary>
    public async Task RevokeFamilyImmediatelyAsync(Guid familyId, DateTime now, CancellationToken cancellationToken)
    {
        await using NpgsqlConnection connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using NpgsqlCommand command = new(
            "UPDATE identity.refresh_tokens SET revoked_at = $2 WHERE family_id = $1 AND revoked_at IS NULL",
            connection);
        command.Parameters.Add(new NpgsqlParameter { Value = familyId });
        command.Parameters.Add(new NpgsqlParameter { Value = now });
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}

internal sealed class InvitationRepository(IDbSession db)
{
    private const string Columns = "id, trainer_id, email, first_name, last_name, message, token_hash, status, created_at, expires_at, accepted_at, cancelled_at, client_id";

    public async Task<Invitation?> GetForTrainerAsync(Guid id, Guid trainerId, CancellationToken cancellationToken)
    {
        InvitationRecord? record = await db.QuerySingleOrDefaultAsync<InvitationRecord>(
            $"SELECT {Columns} FROM identity.invitations WHERE id = @Id AND trainer_id = @TrainerId",
            new { Id = id, TrainerId = trainerId },
            cancellationToken);

        return record is null ? null : Invitation.Restore(record);
    }

    public async Task<Invitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken)
    {
        InvitationRecord? record = await db.QuerySingleOrDefaultAsync<InvitationRecord>(
            $"SELECT {Columns} FROM identity.invitations WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash },
            cancellationToken);

        return record is null ? null : Invitation.Restore(record);
    }

    public Task<bool> HasActivePendingAsync(Guid trainerId, string email, DateTime now, CancellationToken cancellationToken) =>
        db.QuerySingleAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM identity.invitations
                WHERE trainer_id = @TrainerId AND email = @Email AND status = 'Pending' AND expires_at > @Now)
            """,
            new { TrainerId = trainerId, Email = email, Now = now },
            cancellationToken);

    public Task AddAsync(Invitation invitation, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO identity.invitations (id, trainer_id, email, first_name, last_name, message, token_hash, status, created_at, expires_at)
            VALUES (@Id, @TrainerId, @Email, @FirstName, @LastName, @Message, @TokenHash, @Status, @CreatedAt, @ExpiresAt)
            """,
            new
            {
                invitation.Id,
                invitation.TrainerId,
                invitation.Email,
                invitation.FirstName,
                invitation.LastName,
                invitation.Message,
                invitation.TokenHash,
                Status = invitation.StoredStatus.ToString(),
                invitation.CreatedAt,
                invitation.ExpiresAt,
            },
            cancellationToken);

    public Task UpdateAsync(Invitation invitation, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            UPDATE identity.invitations
            SET token_hash = @TokenHash, status = @Status, expires_at = @ExpiresAt, accepted_at = @AcceptedAt, cancelled_at = @CancelledAt, client_id = @ClientId
            WHERE id = @Id
            """,
            new
            {
                invitation.Id,
                invitation.TokenHash,
                Status = invitation.StoredStatus.ToString(),
                invitation.ExpiresAt,
                invitation.AcceptedAt,
                invitation.CancelledAt,
                invitation.ClientId,
            },
            cancellationToken);
}

internal sealed record PasswordResetRecord
{
    public Guid Id { get; init; }

    public Guid UserId { get; init; }

    public DateTime ExpiresAt { get; init; }

    public DateTime? UsedAt { get; init; }
}

internal sealed class PasswordResetRepository(IDbSession db)
{
    public Task AddAsync(Guid id, Guid userId, string tokenHash, DateTime now, DateTime expiresAt, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO identity.password_reset_tokens (id, user_id, token_hash, created_at, expires_at)
            VALUES (@Id, @UserId, @TokenHash, @Now, @ExpiresAt)
            """,
            new { Id = id, UserId = userId, TokenHash = tokenHash, Now = now, ExpiresAt = expiresAt },
            cancellationToken);

    public Task<PasswordResetRecord?> FindByHashAsync(string tokenHash, CancellationToken cancellationToken) =>
        db.QuerySingleOrDefaultAsync<PasswordResetRecord>(
            "SELECT id, user_id, expires_at, used_at FROM identity.password_reset_tokens WHERE token_hash = @TokenHash",
            new { TokenHash = tokenHash },
            cancellationToken);

    /// <summary>
    /// Marks every outstanding reset token of the user as used, so one reset invalidates all earlier links.
    /// </summary>
    public Task UseAllForUserAsync(Guid userId, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            "UPDATE identity.password_reset_tokens SET used_at = @Now WHERE user_id = @UserId AND used_at IS NULL",
            new { UserId = userId, Now = now },
            cancellationToken);
}
