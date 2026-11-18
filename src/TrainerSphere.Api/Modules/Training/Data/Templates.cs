using Npgsql;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;

namespace TrainerSphere.Api.Modules.Training.Data;

internal interface IExerciseRepository
{
    Task<Exercise?> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Exercise>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    Task<bool> NameInUseAsync(Guid trainerId, string name, Guid? exceptId, CancellationToken cancellationToken);

    /// <summary>
    /// Inserts the exercise; returns false when a concurrent request took the name.
    /// </summary>
    Task<bool> TryAddAsync(Exercise exercise, CancellationToken cancellationToken);

    Task<bool> TryUpdateAsync(Exercise exercise, CancellationToken cancellationToken);
}

internal interface IWorkoutRepository
{
    Task<Workout?> GetAsync(Guid id, Guid trainerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workout>> GetManyAsync(IReadOnlyCollection<Guid> ids, Guid trainerId, CancellationToken cancellationToken);

    Task AddAsync(Workout workout, CancellationToken cancellationToken);

    Task UpdateAsync(Workout workout, CancellationToken cancellationToken);
}

internal interface IProgramRepository
{
    Task<TrainingProgram?> GetAsync(Guid id, Guid trainerId, CancellationToken cancellationToken);

    Task AddAsync(TrainingProgram program, CancellationToken cancellationToken);

    Task UpdateAsync(TrainingProgram program, CancellationToken cancellationToken);

    Task AddAssignmentAsync(ProgramAssignment assignment, CancellationToken cancellationToken);

    /// <summary>
    /// Gets a program assignment of the trainer that was not removed.
    /// </summary>
    Task<ProgramAssignment?> GetAssignmentAsync(Guid id, Guid trainerId, CancellationToken cancellationToken);

    Task RemoveAssignmentAsync(Guid id, DateTime now, CancellationToken cancellationToken);
}

internal static class ExerciseSql
{
    public const string Columns = "id, trainer_id, slug, name, category, movement_pattern, primary_muscles, secondary_muscles, equipment, unilateral, cue, illustration, archived, created_at, updated_at";
}

internal sealed class ExerciseRepository(IDbSession db) : IExerciseRepository
{
    public async Task<Exercise?> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        ExerciseRecord? record = await db.QuerySingleOrDefaultAsync<ExerciseRecord>(
            $"SELECT {ExerciseSql.Columns} FROM training.exercises WHERE id = @Id",
            new { Id = id },
            cancellationToken);

        return record is null ? null : Exercise.Restore(record);
    }

    public async Task<IReadOnlyList<Exercise>> GetManyAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        IReadOnlyList<ExerciseRecord> records = await db.QueryAsync<ExerciseRecord>(
            $"SELECT {ExerciseSql.Columns} FROM training.exercises WHERE id = ANY(@Ids)",
            new { Ids = ids.Distinct().ToArray() },
            cancellationToken);

        return [.. records.Select(Exercise.Restore)];
    }

    public Task<bool> NameInUseAsync(Guid trainerId, string name, Guid? exceptId, CancellationToken cancellationToken) =>
        db.QuerySingleAsync<bool>(
            """
            SELECT EXISTS (
                SELECT 1 FROM training.exercises
                WHERE trainer_id = @TrainerId AND NOT archived AND lower(name) = lower(@Name) AND (@ExceptId::uuid IS NULL OR id <> @ExceptId::uuid))
            """,
            new { TrainerId = trainerId, Name = name, ExceptId = exceptId },
            cancellationToken);

    public async Task<bool> TryAddAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        try
        {
            await db.ExecuteAsync(
                """
                INSERT INTO training.exercises (id, trainer_id, slug, name, category, movement_pattern, primary_muscles, secondary_muscles, equipment, unilateral, cue, illustration, archived, created_at, updated_at)
                VALUES (@Id, @TrainerId, @Slug, @Name, @Category, @MovementPattern, @PrimaryMuscles, @SecondaryMuscles, @Equipment, @Unilateral, @Cue, @Illustration, @Archived, @CreatedAt, @UpdatedAt)
                """,
                Parameters(exercise),
                cancellationToken);

            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    public async Task<bool> TryUpdateAsync(Exercise exercise, CancellationToken cancellationToken)
    {
        try
        {
            await db.ExecuteAsync(
                """
                UPDATE training.exercises
                SET name = @Name, category = @Category, movement_pattern = @MovementPattern, primary_muscles = @PrimaryMuscles,
                    secondary_muscles = @SecondaryMuscles, equipment = @Equipment, unilateral = @Unilateral, cue = @Cue,
                    archived = @Archived, updated_at = @UpdatedAt
                WHERE id = @Id
                """,
                Parameters(exercise),
                cancellationToken);

            return true;
        }
        catch (PostgresException ex) when (ex.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            return false;
        }
    }

    private static object Parameters(Exercise exercise) =>
        new
        {
            exercise.Id,
            exercise.TrainerId,
            exercise.Slug,
            exercise.Name,
            exercise.Category,
            exercise.MovementPattern,
            PrimaryMuscles = exercise.PrimaryMuscles.ToArray(),
            SecondaryMuscles = exercise.SecondaryMuscles.ToArray(),
            Equipment = exercise.Equipment.ToArray(),
            exercise.Unilateral,
            exercise.Cue,
            exercise.Illustration,
            exercise.Archived,
            exercise.CreatedAt,
            exercise.UpdatedAt,
        };
}

internal sealed class WorkoutRepository(IDbSession db) : IWorkoutRepository
{
    public async Task<Workout?> GetAsync(Guid id, Guid trainerId, CancellationToken cancellationToken)
    {
        IReadOnlyList<Workout> workouts = await GetManyAsync([id], trainerId, cancellationToken);
        return workouts.Count == 0 ? null : workouts[0];
    }

    public async Task<IReadOnlyList<Workout>> GetManyAsync(IReadOnlyCollection<Guid> ids, Guid trainerId, CancellationToken cancellationToken)
    {
        Guid[] distinct = [.. ids.Distinct()];

        IReadOnlyList<WorkoutRecord> headers = await db.QueryAsync<WorkoutRecord>(
            "SELECT id, trainer_id, name, description, archived, created_at, updated_at FROM training.workouts WHERE id = ANY(@Ids) AND trainer_id = @TrainerId",
            new { Ids = distinct, TrainerId = trainerId },
            cancellationToken);

        if (headers.Count == 0)
        {
            return [];
        }

        IReadOnlyList<WorkoutExerciseRow> lines = await db.QueryAsync<WorkoutExerciseRow>(
            """
            SELECT id, workout_id, position, exercise_id, sets, goal_type, goal_min, goal_max, load_type, load_value, rest_seconds, tempo, superset_group, notes
            FROM training.workout_exercises
            WHERE workout_id = ANY(@Ids)
            ORDER BY workout_id, position
            """,
            new { Ids = headers.Select(h => h.Id).ToArray() },
            cancellationToken);

        ILookup<Guid, WorkoutExerciseRow> byWorkout = lines.ToLookup(l => l.WorkoutId);

        return [.. headers.Select(h => Workout.Restore(h, byWorkout[h.Id].Select(l => l.ToDomain())))];
    }

    public async Task AddAsync(Workout workout, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            """
            INSERT INTO training.workouts (id, trainer_id, name, description, archived, created_at, updated_at)
            VALUES (@Id, @TrainerId, @Name, @Description, @Archived, @CreatedAt, @UpdatedAt)
            """,
            new { workout.Id, workout.TrainerId, workout.Name, workout.Description, workout.Archived, workout.CreatedAt, workout.UpdatedAt },
            cancellationToken);
        await InsertLinesAsync(workout, cancellationToken);
    }

    public async Task UpdateAsync(Workout workout, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            "UPDATE training.workouts SET name = @Name, description = @Description, archived = @Archived, updated_at = @UpdatedAt WHERE id = @Id",
            new { workout.Id, workout.Name, workout.Description, workout.Archived, workout.UpdatedAt },
            cancellationToken);
        await db.ExecuteAsync("DELETE FROM training.workout_exercises WHERE workout_id = @Id", new { workout.Id }, cancellationToken);
        await InsertLinesAsync(workout, cancellationToken);
    }

    private async Task InsertLinesAsync(Workout workout, CancellationToken cancellationToken)
    {
        if (workout.Exercises.Count == 0)
        {
            return;
        }

        await db.ExecuteAsync(
            """
            INSERT INTO training.workout_exercises (id, workout_id, position, exercise_id, sets, goal_type, goal_min, goal_max, load_type, load_value, rest_seconds, tempo, superset_group, notes)
            VALUES (@Id, @WorkoutId, @Position, @ExerciseId, @Sets, @GoalType, @GoalMin, @GoalMax, @LoadType, @LoadValue, @RestSeconds, @Tempo, @SupersetGroup, @Notes)
            """,
            workout.Exercises.Select(e => new
            {
                e.Id,
                WorkoutId = workout.Id,
                e.Position,
                e.ExerciseId,
                e.Sets,
                GoalType = e.Goal.Type.ToString(),
                GoalMin = e.Goal.Min,
                GoalMax = e.Goal.Max,
                LoadType = e.Load.Type.ToString(),
                LoadValue = e.Load.Value,
                e.RestSeconds,
                e.Tempo,
                e.SupersetGroup,
                e.Notes,
            }).ToList(),
            cancellationToken);
    }

    internal sealed record WorkoutExerciseRow
    {
        public Guid Id { get; init; }

        public Guid WorkoutId { get; init; }

        public int Position { get; init; }

        public Guid ExerciseId { get; init; }

        public int Sets { get; init; }

        public string GoalType { get; init; } = string.Empty;

        public int GoalMin { get; init; }

        public int GoalMax { get; init; }

        public string LoadType { get; init; } = string.Empty;

        public decimal? LoadValue { get; init; }

        public int RestSeconds { get; init; }

        public string? Tempo { get; init; }

        public string? SupersetGroup { get; init; }

        public string? Notes { get; init; }

        public WorkoutExercise ToDomain() =>
            new(
                Id,
                Position,
                ExerciseId,
                Sets,
                new Goal(Enum.Parse<Domain.GoalType>(GoalType), GoalMin, GoalMax),
                new Load(Enum.Parse<Domain.LoadType>(LoadType), LoadValue),
                RestSeconds,
                Tempo,
                SupersetGroup,
                Notes);
    }
}

internal sealed class ProgramRepository(IDbSession db) : IProgramRepository
{
    public async Task<TrainingProgram?> GetAsync(Guid id, Guid trainerId, CancellationToken cancellationToken)
    {
        ProgramRecord? record = await db.QuerySingleOrDefaultAsync<ProgramRecord>(
            "SELECT id, trainer_id, name, description, weeks, archived, created_at, updated_at FROM training.programs WHERE id = @Id AND trainer_id = @TrainerId",
            new { Id = id, TrainerId = trainerId },
            cancellationToken);

        if (record is null)
        {
            return null;
        }

        IReadOnlyList<ProgramSlotRow> slots = await db.QueryAsync<ProgramSlotRow>(
            "SELECT id, week, day, ordinal, workout_id FROM training.program_slots WHERE program_id = @Id",
            new { Id = id },
            cancellationToken);

        return TrainingProgram.Restore(record, slots.Select(s => new ProgramSlot(s.Id, s.Week, s.Day, s.Ordinal, s.WorkoutId)));
    }

    public async Task AddAsync(TrainingProgram program, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            """
            INSERT INTO training.programs (id, trainer_id, name, description, weeks, archived, created_at, updated_at)
            VALUES (@Id, @TrainerId, @Name, @Description, @Weeks, @Archived, @CreatedAt, @UpdatedAt)
            """,
            new { program.Id, program.TrainerId, program.Name, program.Description, program.Weeks, program.Archived, program.CreatedAt, program.UpdatedAt },
            cancellationToken);
        await InsertSlotsAsync(program, cancellationToken);
    }

    public async Task UpdateAsync(TrainingProgram program, CancellationToken cancellationToken)
    {
        await db.ExecuteAsync(
            "UPDATE training.programs SET name = @Name, description = @Description, weeks = @Weeks, archived = @Archived, updated_at = @UpdatedAt WHERE id = @Id",
            new { program.Id, program.Name, program.Description, program.Weeks, program.Archived, program.UpdatedAt },
            cancellationToken);
        await db.ExecuteAsync("DELETE FROM training.program_slots WHERE program_id = @Id", new { program.Id }, cancellationToken);
        await InsertSlotsAsync(program, cancellationToken);
    }

    public Task AddAssignmentAsync(ProgramAssignment assignment, CancellationToken cancellationToken) =>
        db.ExecuteAsync(
            """
            INSERT INTO training.program_assignments (id, program_id, program_name, trainer_id, client_id, start_date, end_date, created_at)
            VALUES (@Id, @ProgramId, @ProgramName, @TrainerId, @ClientId, @StartDate, @EndDate, @CreatedAt)
            """,
            assignment,
            cancellationToken);

    public async Task<ProgramAssignment?> GetAssignmentAsync(Guid id, Guid trainerId, CancellationToken cancellationToken)
    {
        ProgramAssignmentRow? row = await db.QuerySingleOrDefaultAsync<ProgramAssignmentRow>(
            """
            SELECT id, program_id, program_name, trainer_id, client_id, start_date, end_date, created_at
            FROM training.program_assignments
            WHERE id = @Id AND trainer_id = @TrainerId AND removed_at IS NULL
            """,
            new { Id = id, TrainerId = trainerId },
            cancellationToken);

        return row is null ? null : new ProgramAssignment(row.Id, row.ProgramId, row.ProgramName, row.TrainerId, row.ClientId, row.StartDate, row.EndDate, row.CreatedAt);
    }

    public Task RemoveAssignmentAsync(Guid id, DateTime now, CancellationToken cancellationToken) =>
        db.ExecuteAsync("UPDATE training.program_assignments SET removed_at = @Now WHERE id = @Id", new { Id = id, Now = now }, cancellationToken);

    private async Task InsertSlotsAsync(TrainingProgram program, CancellationToken cancellationToken)
    {
        if (program.Slots.Count == 0)
        {
            return;
        }

        await db.ExecuteAsync(
            "INSERT INTO training.program_slots (id, program_id, week, day, ordinal, workout_id) VALUES (@Id, @ProgramId, @Week, @Day, @Ordinal, @WorkoutId)",
            program.Slots.Select(s => new { s.Id, ProgramId = program.Id, s.Week, s.Day, s.Ordinal, s.WorkoutId }).ToList(),
            cancellationToken);
    }

    internal sealed record ProgramSlotRow
    {
        public Guid Id { get; init; }

        public int Week { get; init; }

        public int Day { get; init; }

        public int Ordinal { get; init; }

        public Guid WorkoutId { get; init; }
    }

    internal sealed record ProgramAssignmentRow
    {
        public Guid Id { get; init; }

        public Guid ProgramId { get; init; }

        public string ProgramName { get; init; } = string.Empty;

        public Guid TrainerId { get; init; }

        public Guid ClientId { get; init; }

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public DateTime CreatedAt { get; init; }
    }
}
