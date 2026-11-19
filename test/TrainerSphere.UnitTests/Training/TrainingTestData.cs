using Dapper;
using Npgsql;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.UnitTests.Training;

internal static class TrainingTestData
{
    public static readonly Guid TrainerId = new("11111111-1111-1111-1111-111111111111");
    public static readonly Guid ClientId = new("22222222-2222-2222-2222-222222222222");
    public static readonly DateTime Now = new(2026, 11, 18, 12, 0, 0, DateTimeKind.Utc);
    public static readonly DateOnly Today = new(2026, 11, 18);

    public static ClientProfile Client(
        Guid? id = null,
        Guid? trainerId = null,
        ClientStatus status = ClientStatus.Active,
        string timeZone = "UTC",
        string firstName = "Alex",
        string lastName = "Rivera",
        string? goals = null,
        string? trainerNotes = null) =>
        ClientProfile.Restore(new ClientRecord
        {
            Id = id ?? ClientId,
            TrainerId = trainerId ?? TrainerId,
            Email = "alex@example.com",
            FirstName = firstName,
            LastName = lastName,
            TimeZone = timeZone,
            Status = status.ToString(),
            JoinedAt = new DateTime(2026, 10, 3, 12, 0, 0, DateTimeKind.Utc),
            ArchivedAt = status == ClientStatus.Archived ? Now : null,
            Goals = goals,
            TrainerNotes = trainerNotes,
            UpdatedAt = Now,
        });

    public static Workout Workout(Guid? id = null, Guid? trainerId = null, string name = "Lower A") =>
        TrainerSphere.Api.Modules.Training.Domain.Workout.Create(
            id ?? Guid.NewGuid(),
            trainerId ?? TrainerId,
            new WorkoutDefinition(name, null, []),
            Guid.NewGuid,
            Now);

    public static Assignment ScheduledAssignment(ClientProfile? client = null, DateOnly? date = null, string? notes = null)
    {
        ClientProfile owner = client ?? Client();
        Workout workout = Workout(trainerId: owner.TrainerId);

        return Assignment.Schedule(
            Guid.NewGuid(),
            owner,
            workout.Id,
            workout.Snapshot(new Dictionary<Guid, ExerciseSummary>()),
            date ?? Today,
            notes,
            program: null,
            Now);
    }
}

/// <summary>
/// Returns empty results for every query; the handlers under test only read through it for projections that are
/// empty in these scenarios.
/// </summary>
internal sealed class FakeDbSession : IDbSession
{
    public bool HasTransaction => false;

    public Task<NpgsqlConnection> GetConnectionAsync(CancellationToken cancellationToken) => throw new NotSupportedException();

    public Task BeginTransactionAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task RollbackAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public void OnCommitted(Action action) => action();

    public Task<IReadOnlyList<T>> QueryAsync<T>(string sql, object? param, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<T>>([]);

    public Task<T?> QuerySingleOrDefaultAsync<T>(string sql, object? param, CancellationToken cancellationToken) =>
        Task.FromResult(default(T));

    public Task<T> QuerySingleAsync<T>(string sql, object? param, CancellationToken cancellationToken) =>
        Task.FromResult(default(T)!);

    public Task<T?> ExecuteScalarAsync<T>(string sql, object? param, CancellationToken cancellationToken) =>
        Task.FromResult(default(T));

    public Task<int> ExecuteAsync(string sql, object? param, CancellationToken cancellationToken) =>
        Task.FromResult(0);

    public Task<SqlMapper.GridReader> QueryMultipleAsync(string sql, object? param, CancellationToken cancellationToken) =>
        throw new NotSupportedException();
}
