using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.Api.Modules.Training.Features.Assignments;
using TrainerSphere.Api.Modules.Training.Features.BodyMetrics;
using TrainerSphere.Api.Modules.Training.Features.Programs;
using TrainerSphere.Api.Modules.Training.Features.Sessions;
using TrainerSphere.Api.Modules.Training.Features.Workouts;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features;

/// <summary>
/// The demo gym story: five workouts, a repeating four-week program (one finished cycle, one in progress) for
/// Alex, Sam and Jordan, about six weeks of logged sessions with progressive weights (so personal records and
/// streaks look real), a few skipped and missed assignments for varied adherence, and weekly body metrics.
/// Lives in the Features root because it orchestrates several slices through their own commands and queries.
/// Runs after the Identity seed, whose drained events created the trainer and client projections read here.
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("Maintainability", "CA1506:Avoid excessive class coupling", Justification = "A demo seeder is the one place that deliberately touches every slice of the module.")]
internal sealed class TrainingDemoSeeder(
    IDbSession db,
    IClientRepository clients,
    ICommandHandler<CreateWorkoutCommand, WorkoutResponse> createWorkout,
    ICommandHandler<CreateProgramCommand, ProgramResponse> createProgram,
    ICommandHandler<AssignProgramCommand, IReadOnlyList<ProgramAssignmentResponse>> assignProgram,
    IQueryHandler<ListAssignmentsQuery, IReadOnlyList<AssignmentResponse>> listAssignments,
    ICommandHandler<StartSessionCommand, StartedSession> startSession,
    ICommandHandler<UpsertSetCommand, SetResponse> upsertSet,
    ICommandHandler<CompleteSessionCommand, SessionResponse> completeSession,
    ICommandHandler<SkipAssignmentCommand, AssignmentResponse> skipAssignment,
    ICommandHandler<CreateAssignmentCommand, AssignmentResponse> createAssignment,
    ICommandHandler<PutBodyMetricCommand, BodyMetricResponse> putBodyMetric,
    TimeProvider timeProvider) : IDemoSeeder
{
    private const int ProgramWeeks = 4;
    private const int PastWeeks = 6;

    private static readonly int[] SessionMinutes = [40, 47, 54, 61];
    private static readonly TimeOnly SessionStart = new(17, 30);
    private static readonly string[] SkipReasons = ["Work trip", "Felt ill", "Family emergency"];

    /// <summary>Strength multiplier per client, and the past-assignment indexes (0 = oldest) they skip or miss.</summary>
    private static readonly (string Email, decimal Strength, int[] Skip, int[] Miss)[] ClientPlan =
    [
        ("alex@trainersphere.dev", 1.00m, [11], []),
        ("sam@trainersphere.dev", 0.80m, [4, 12], [16]),
        ("jordan@trainersphere.dev", 0.65m, [2, 9, 15], [6, 13]),
    ];

    private static readonly (decimal Weight, decimal BodyFat, decimal Waist, int HeartRate, decimal WeeklyWeight, decimal WeeklyBodyFat, decimal WeeklyWaist)[] MetricsPlan =
    [
        (78.0m, 18.5m, 84m, 58, -0.30m, -0.20m, -0.30m),
        (65.0m, 22.5m, 76m, 62, +0.15m, +0.10m, 0.00m),
        (92.0m, 29.0m, 99m, 66, -0.50m, -0.30m, -0.50m),
    ];

    /// <summary>Week-1 working weights (kg); each logged week adds 2.5 kg after the client's strength multiplier.</summary>
    private static readonly Dictionary<string, decimal> BaseWeights = new(StringComparer.Ordinal)
    {
        ["barbell-back-squat"] = 60m,
        ["leg-press"] = 100m,
        ["lying-leg-curl"] = 30m,
        ["standing-calf-raise"] = 40m,
        ["barbell-romanian-deadlift"] = 60m,
        ["bulgarian-split-squat"] = 16m,
        ["leg-extension"] = 35m,
        ["barbell-bench-press"] = 45m,
        ["barbell-bent-over-row"] = 40m,
        ["seated-dumbbell-shoulder-press"] = 14m,
        ["cable-triceps-pushdown"] = 20m,
        ["incline-dumbbell-press"] = 16m,
        ["lat-pulldown"] = 40m,
        ["dumbbell-biceps-curl"] = 12m,
        ["kettlebell-swing"] = 16m,
        ["goblet-squat"] = 16m,
    };

    public int Order => 20;

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        Guid? trainerId = await db.QuerySingleOrDefaultAsync<Guid?>(
            "SELECT id FROM training.trainers ORDER BY registered_at LIMIT 1",
            param: null,
            cancellationToken);
        if (trainerId is not { } trainer)
        {
            return false;
        }

        bool alreadySeeded = await db.QuerySingleAsync<bool>(
            "SELECT EXISTS (SELECT 1 FROM training.workouts WHERE trainer_id = @TrainerId)",
            new { TrainerId = trainer },
            cancellationToken);
        if (alreadySeeded)
        {
            return false;
        }

        TimeZoneInfo zone = TimeZones.FindOrUtc(DemoAccounts.TimeZone);
        DateOnly today = timeProvider.Today(DemoAccounts.TimeZone);
        DateOnly firstStart = IsoWeek.Start(today.AddDays(-7 * PastWeeks));

        var roster = (await clients.ListAsync(trainer, search: null, status: null, cancellationToken))
            .ToDictionary(c => c.Email, StringComparer.OrdinalIgnoreCase);

        Guid lowerA = await WorkoutAsync(
            trainer,
            "Lower A",
            "Squat-focused lower body.",
            [
                Weighted("barbell-back-squat", 4, 4, 6, 150, "3-1-X-0"),
                Weighted("leg-press", 3, 8, 12, 120),
                Weighted("lying-leg-curl", 3, 10, 15, 90),
                Weighted("standing-calf-raise", 3, 12, 15, 60),
            ],
            cancellationToken);
        Guid lowerB = await WorkoutAsync(
            trainer,
            "Lower B",
            "Hinge-focused lower body.",
            [
                Weighted("barbell-romanian-deadlift", 4, 6, 8, 150, "3-0-X-0"),
                Weighted("bulgarian-split-squat", 3, 8, 10, 120),
                Weighted("leg-extension", 3, 12, 15, 90),
                Bodyweight("hanging-leg-raise", 3, 8, 12, 60),
            ],
            cancellationToken);
        Guid upperA = await WorkoutAsync(
            trainer,
            "Upper A",
            "Bench and row strength.",
            [
                Weighted("barbell-bench-press", 4, 5, 8, 150),
                Weighted("barbell-bent-over-row", 4, 6, 10, 120),
                Weighted("seated-dumbbell-shoulder-press", 3, 8, 12, 90),
                Weighted("cable-triceps-pushdown", 3, 10, 15, 60),
            ],
            cancellationToken);
        Guid upperB = await WorkoutAsync(
            trainer,
            "Upper B",
            "Pull-ups and pressing volume.",
            [
                Bodyweight("pull-up", 4, 5, 8, 150),
                Weighted("incline-dumbbell-press", 3, 8, 12, 120),
                Weighted("lat-pulldown", 3, 10, 12, 90),
                Weighted("dumbbell-biceps-curl", 3, 10, 15, 60),
                Timed("plank", 3, 30, 90, 45),
            ],
            cancellationToken);
        Guid fullBody = await WorkoutAsync(
            trainer,
            "Full Body Conditioning",
            "Kettlebell and bodyweight circuit.",
            [
                Weighted("kettlebell-swing", 4, 15, 20, 90),
                Weighted("goblet-squat", 3, 10, 12, 90),
                Bodyweight("push-up", 3, 10, 20, 60),
                Timed("jump-rope", 3, 60, 180, 60),
            ],
            cancellationToken);

        ProgramResponse created = await Unwrap(
            createProgram.Handle(
                new CreateProgramCommand(
                    trainer,
                    "Strong Start",
                    "Four weeks of full-body fundamentals, three sessions per week. Lower, upper and conditioning days alternate A and B variants.",
                    ProgramWeeks,
                    [
                        new ProgramSlotRequest(1, 1, lowerA),
                        new ProgramSlotRequest(1, 3, upperA),
                        new ProgramSlotRequest(1, 5, fullBody),
                        new ProgramSlotRequest(2, 1, lowerB),
                        new ProgramSlotRequest(2, 3, upperB),
                        new ProgramSlotRequest(2, 5, fullBody),
                        new ProgramSlotRequest(3, 1, lowerA),
                        new ProgramSlotRequest(3, 3, upperA),
                        new ProgramSlotRequest(3, 5, fullBody),
                        new ProgramSlotRequest(4, 1, lowerB),
                        new ProgramSlotRequest(4, 3, upperB),
                        new ProgramSlotRequest(4, 5, fullBody),
                    ]),
                cancellationToken),
            "create program");

        Guid[] clientIds = [.. ClientPlan.Select(plan => roster[plan.Email].Id)];
        await Unwrap(assignProgram.Handle(new AssignProgramCommand(trainer, created.Id, clientIds, firstStart), cancellationToken), "assign cycle one");
        await Unwrap(assignProgram.Handle(new AssignProgramCommand(trainer, created.Id, clientIds, firstStart.AddDays(7 * ProgramWeeks)), cancellationToken), "assign cycle two");

        for (int index = 0; index < ClientPlan.Length; index++)
        {
            (string email, decimal strength, int[] skip, int[] miss) = ClientPlan[index];
            ClientProfile client = roster[email];
            await SeedSessionsAsync(trainer, client, strength, skip, miss, firstStart, today, zone, cancellationToken);
            await SeedBodyMetricsAsync(trainer, client.Id, MetricsPlan[index], firstStart, today, cancellationToken);
            await EnsureTodayAssignmentAsync(trainer, client.Id, lowerA, today, cancellationToken);
        }

        return true;
    }

    private static (decimal? Weight, int? Reps, int? Duration) Prescription(WorkoutExerciseView exercise, int week, int setNumber, decimal strength)
    {
        if (exercise.Goal.Type == GoalType.Time)
        {
            int seconds = Math.Min(exercise.Goal.Min + (week * 10) + (setNumber * 5), exercise.Goal.Max);
            return (null, null, seconds);
        }

        int midpoint = exercise.Goal.Min + ((exercise.Goal.Max - exercise.Goal.Min) / 2);
        int performed = midpoint + ((week + setNumber) % 2);
        decimal? weight = exercise.Load.Type == LoadType.Weight
            ? RoundToNearest((BaseWeights.GetValueOrDefault(exercise.Exercise.Slug ?? string.Empty, 20m) * strength) + (2.5m * week), 2.5m)
            : null;
        return (weight, performed, null);
    }

    private static decimal RoundToNearest(decimal value, decimal step) => Math.Round(value / step, MidpointRounding.ToEven) * step;

    private static WorkoutExerciseRequest Weighted(string slug, int sets, int min, int max, int restSeconds, string? tempo = null) =>
        new(ExerciseLibrary.IdFor(slug), sets, new GoalRequest("reps", min, max), new LoadRequest("weight", BaseWeights[slug]), restSeconds, tempo, SupersetGroup: null, Notes: null);

    private static WorkoutExerciseRequest Bodyweight(string slug, int sets, int min, int max, int restSeconds) =>
        new(ExerciseLibrary.IdFor(slug), sets, new GoalRequest("reps", min, max), new LoadRequest("bodyweight", null), restSeconds, Tempo: null, SupersetGroup: null, Notes: null);

    private static WorkoutExerciseRequest Timed(string slug, int sets, int minSeconds, int maxSeconds, int restSeconds) =>
        new(ExerciseLibrary.IdFor(slug), sets, new GoalRequest("time", minSeconds, maxSeconds), new LoadRequest("none", null), restSeconds, Tempo: null, SupersetGroup: null, Notes: null);

    private static async Task<T> Unwrap<T>(Task<Result<T>> result, string what)
        where T : notnull =>
        (await result).Match(
            value => value,
            error => throw new InvalidOperationException($"Demo seed: {what} failed with {error.Code}: {error.Message}"));

    private async Task<Guid> WorkoutAsync(Guid trainer, string name, string description, WorkoutExerciseRequest[] exercises, CancellationToken cancellationToken)
    {
        WorkoutResponse created = await Unwrap(
            createWorkout.Handle(new CreateWorkoutCommand(trainer, name, description, exercises), cancellationToken),
            $"create workout {name}");
        return created.Id;
    }

    private async Task SeedSessionsAsync(
        Guid trainer,
        ClientProfile client,
        decimal strength,
        int[] skip,
        int[] miss,
        DateOnly firstStart,
        DateOnly today,
        TimeZoneInfo zone,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AssignmentResponse> past = await Unwrap(
            listAssignments.Handle(new ListAssignmentsQuery(Viewer.Trainer(trainer), client.Id, firstStart, today.AddDays(-1), Status: null), cancellationToken),
            "list assignments");

        int completed = 0;

        for (int i = 0; i < past.Count; i++)
        {
            AssignmentResponse assignment = past[i];

            if (skip.Contains(i))
            {
                await Unwrap(
                    skipAssignment.Handle(new SkipAssignmentCommand(Viewer.Trainer(trainer), assignment.Id, SkipReasons[i % SkipReasons.Length]), cancellationToken),
                    "skip assignment");
                continue;
            }

            if (miss.Contains(i))
            {
                continue;
            }

            await LogSessionAsync(client.Id, strength, assignment, firstStart, zone, completed, cancellationToken);
            completed++;
        }
    }

    private async Task LogSessionAsync(
        Guid clientId,
        decimal strength,
        AssignmentResponse assignment,
        DateOnly firstStart,
        TimeZoneInfo zone,
        int completed,
        CancellationToken cancellationToken)
    {
        DateTime startedAt = TimeZoneInfo.ConvertTimeToUtc(assignment.Date.ToDateTime(SessionStart), zone);
        StartedSession started = await Unwrap(
            startSession.Handle(new StartSessionCommand(clientId, Id: null, assignment.Id, WorkoutName: null, startedAt), cancellationToken),
            "start session");

        int week = (assignment.Date.DayNumber - firstStart.DayNumber) / 7;

        foreach (WorkoutExerciseView exercise in assignment.Workout.Exercises)
        {
            for (int setNumber = 1; setNumber <= exercise.Sets; setNumber++)
            {
                (decimal? weight, int? reps, int? duration) = Prescription(exercise, week, setNumber, strength);
                decimal topSetRpe = week % 2 == 0 ? 8m : 8.5m;
                decimal? rpe = setNumber == exercise.Sets ? topSetRpe : null;

                await Unwrap(
                    upsertSet.Handle(
                        new UpsertSetCommand(
                            clientId,
                            started.Session.Id,
                            Ids.New(timeProvider),
                            exercise.ExerciseId,
                            exercise.Position,
                            setNumber,
                            weight,
                            reps,
                            duration,
                            DistanceMeters: null,
                            rpe,
                            Completed: true,
                            Notes: null),
                        cancellationToken),
                    "log set");
            }
        }

        await Unwrap(
            completeSession.Handle(
                new CompleteSessionCommand(
                    clientId,
                    started.Session.Id,
                    startedAt.AddMinutes(SessionMinutes[completed % SessionMinutes.Length]),
                    completed % 5 == 0 ? "Solid session." : null),
                cancellationToken),
            "complete session");
    }

    /// <summary>Program days are Monday, Wednesday and Friday, so weekends need an ad-hoc assignment for the demo's "today" view.</summary>
    private async Task EnsureTodayAssignmentAsync(Guid trainer, Guid clientId, Guid workoutId, DateOnly today, CancellationToken cancellationToken)
    {
        IReadOnlyList<AssignmentResponse> todays = await Unwrap(
            listAssignments.Handle(new ListAssignmentsQuery(Viewer.Trainer(trainer), clientId, today, today, Status: null), cancellationToken),
            "list today's assignments");

        if (todays.Count == 0)
        {
            await Unwrap(
                createAssignment.Handle(new CreateAssignmentCommand(trainer, clientId, workoutId, today, "Technique check-in."), cancellationToken),
                "assign today");
        }
    }

    private async Task SeedBodyMetricsAsync(
        Guid trainer,
        Guid clientId,
        (decimal Weight, decimal BodyFat, decimal Waist, int HeartRate, decimal WeeklyWeight, decimal WeeklyBodyFat, decimal WeeklyWaist) plan,
        DateOnly firstStart,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        for (int week = 0; week <= PastWeeks; week++)
        {
            DateOnly date = firstStart.AddDays(7 * week);
            if (date > today)
            {
                break;
            }

            await Unwrap(
                putBodyMetric.Handle(
                    new PutBodyMetricCommand(
                        Viewer.Trainer(trainer),
                        clientId,
                        date,
                        plan.Weight + (plan.WeeklyWeight * week),
                        plan.BodyFat + (plan.WeeklyBodyFat * week),
                        plan.Waist + (plan.WeeklyWaist * week),
                        ChestCm: null,
                        HipsCm: null,
                        ArmCm: null,
                        ThighCm: null,
                        plan.HeartRate,
                        week == 0 ? "Baseline check-in." : null),
                    cancellationToken),
                "put body metric");
        }
    }
}
