using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Workouts;

internal interface IWorkoutCommand
{
    string Name { get; }

    string? Description { get; }

    IReadOnlyList<WorkoutExerciseRequest> Exercises { get; }
}

internal sealed record WorkoutSummaryResponse(Guid Id, string Name, string? Description, int ExerciseCount, bool Archived, DateTime UpdatedAt);

internal sealed record GoalRequest(string? Type, int Min, int Max);

internal sealed record LoadRequest(string? Type, decimal? Value);

internal sealed record WorkoutExerciseRequest(
    Guid ExerciseId,
    int Sets,
    GoalRequest? Goal,
    LoadRequest? Load,
    int RestSeconds,
    string? Tempo,
    string? SupersetGroup,
    string? Notes)
{
    public WorkoutExerciseDefinition ToDefinition()
    {
        GoalType goalType = KebabCase.TryParse(Goal?.Type, out GoalType parsedGoal) ? parsedGoal : default;
        LoadType loadType = KebabCase.TryParse(Load?.Type, out LoadType parsedLoad) ? parsedLoad : default;

        return new WorkoutExerciseDefinition(
            ExerciseId,
            Sets,
            new Goal(goalType, Goal?.Min ?? 0, Goal?.Max ?? 0),
            new Load(loadType, Load?.Value),
            RestSeconds,
            Tempo,
            SupersetGroup,
            Notes);
    }
}

internal sealed record WorkoutRequest(string Name, string? Description, IReadOnlyList<WorkoutExerciseRequest>? Exercises);

internal sealed record ListWorkoutsQuery(Guid TrainerId, string? Search, bool IncludeArchived, string? Sort, int Page, int PageSize)
    : IQuery<Paged<WorkoutSummaryResponse>>, IPagedQuery;

internal static class WorkoutViews
{
    public static async Task<WorkoutResponse> ToResponseAsync(Workout workout, IExerciseRepository exercises, CancellationToken cancellationToken) =>
        WorkoutResponse.From(workout, await exercises.GetSummariesAsync(workout.Exercises.Select(e => e.ExerciseId), cancellationToken));

    /// <summary>
    /// Checks that every referenced exercise exists, is visible to the trainer and is not archived. Unknown and
    /// other trainers' exercises are the same 404 <c>Exercise.NotFound</c> (existence is not leaked across
    /// trainers); archived ones are 422 <c>Exercise.Archived</c>, except exercises the workout already uses so
    /// existing workouts remain editable. Both carry per-item errors keyed by the workout line.
    /// </summary>
    public static async Task<Error?> CheckExercisesAsync(
        IReadOnlyList<WorkoutExerciseRequest> lines,
        Guid trainerId,
        IReadOnlySet<Guid> alreadyUsed,
        IExerciseRepository exercises,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Exercise> found = await exercises.GetManyAsync([.. lines.Select(l => l.ExerciseId)], cancellationToken);
        var byId = found.ToDictionary(e => e.Id);
        var trainer = Viewer.Trainer(trainerId);
        List<FieldError> notFound = [];
        List<FieldError> archived = [];

        for (int index = 0; index < lines.Count; index++)
        {
            Guid exerciseId = lines[index].ExerciseId;
            string field = $"exercises[{index}].exerciseId";

            if (!byId.TryGetValue(exerciseId, out Exercise? exercise) || !exercise.IsVisibleTo(trainer))
            {
                NotFoundError error = ExerciseErrors.NotFound(exerciseId);
                notFound.Add(new FieldError(field, error.Code, error.Message));
            }
            else if (exercise.Archived && !alreadyUsed.Contains(exerciseId))
            {
                DomainError error = ExerciseErrors.Archived(exerciseId);
                archived.Add(new FieldError(field, error.Code, error.Message));
            }
        }

        if (notFound.Count > 0)
        {
            return ExerciseErrors.NotFound(notFound);
        }

        return archived.Count > 0 ? ExerciseErrors.Archived(archived) : null;
    }

    public static WorkoutDefinition ToDefinition(string name, string? description, IReadOnlyList<WorkoutExerciseRequest> lines) =>
        new(name, description, [.. lines.Select(l => l.ToDefinition())]);
}

internal sealed class ListWorkoutsValidator : PagedQueryValidator<ListWorkoutsQuery>
{
    public static readonly IReadOnlyDictionary<string, string> Orders = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["name"] = "lower(w.name), w.id",
        ["-name"] = "lower(w.name) DESC, w.id",
        ["updatedAt"] = "w.updated_at, w.id",
        ["-updatedAt"] = "w.updated_at DESC, w.id",
    };

    public ListWorkoutsValidator()
    {
        RuleFor(q => q.Search).MaximumLength(TrainingRules.SearchMaxLength);
        RuleFor(q => q.Sort)
            .Must(sort => sort is null || Orders.ContainsKey(sort))
            .WithMessage($"Must be one of: {string.Join(", ", Orders.Keys)}.");
    }
}

internal sealed class ListWorkoutsHandler(IDbSession db) : IQueryHandler<ListWorkoutsQuery, Paged<WorkoutSummaryResponse>>
{
    private const string Filter =
        "w.trainer_id = @TrainerId AND (@IncludeArchived OR NOT w.archived) AND (@Search::text IS NULL OR w.name ILIKE @Search::text)";

    public async Task<Result<Paged<WorkoutSummaryResponse>>> Handle(ListWorkoutsQuery query, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            query.TrainerId,
            query.IncludeArchived,
            Search = TrainingRules.LikePattern(query.Search),
            query.PageSize,
            Offset = (query.Page - 1) * query.PageSize,
        };

        string order = ListWorkoutsValidator.Orders[query.Sort ?? "name"];
        int total = await db.QuerySingleAsync<int>($"SELECT count(*)::int FROM training.workouts w WHERE {Filter}", parameters, cancellationToken);
        IReadOnlyList<WorkoutSummaryRow> rows = await db.QueryAsync<WorkoutSummaryRow>(
            $"""
            SELECT w.id, w.name, w.description, w.archived, w.updated_at,
                   (SELECT count(*)::int FROM training.workout_exercises we WHERE we.workout_id = w.id) AS exercise_count
            FROM training.workouts w
            WHERE {Filter}
            ORDER BY {order}
            LIMIT @PageSize OFFSET @Offset
            """,
            parameters,
            cancellationToken);

        return new Paged<WorkoutSummaryResponse>(
            [.. rows.Select(r => new WorkoutSummaryResponse(r.Id, r.Name, r.Description, r.ExerciseCount, r.Archived, r.UpdatedAt))],
            query.Page,
            query.PageSize,
            total);
    }

    internal sealed record WorkoutSummaryRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public int ExerciseCount { get; init; }

        public bool Archived { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
}

internal sealed record GetWorkoutQuery(Viewer Viewer, Guid WorkoutId) : IQuery<WorkoutResponse>;

internal sealed class GetWorkoutHandler(IWorkoutRepository workouts, IExerciseRepository exercises) : IQueryHandler<GetWorkoutQuery, WorkoutResponse>
{
    public async Task<Result<WorkoutResponse>> Handle(GetWorkoutQuery query, CancellationToken cancellationToken)
    {
        // Clients see workouts only as the snapshots in their assignments.
        Workout? workout = query.Viewer.IsTrainer ? await workouts.GetAsync(query.WorkoutId, query.Viewer.UserId, cancellationToken) : null;

        if (workout is null)
        {
            return WorkoutErrors.NotFound(query.WorkoutId);
        }

        return await WorkoutViews.ToResponseAsync(workout, exercises, cancellationToken);
    }
}

internal abstract class WorkoutCommandValidator<T> : AbstractValidator<T>
    where T : IWorkoutCommand
{
    private const string TempoPattern = "^[0-9X]-[0-9X]-[0-9X]-[0-9X]$";

    protected WorkoutCommandValidator()
    {
        RuleFor(c => c.Name).ValidName(WorkoutDefinition.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(WorkoutDefinition.DescriptionMaxLength);
        RuleFor(c => c.Exercises)
            .Must(lines => lines.Count is >= 1 and <= WorkoutDefinition.MaxExercises)
            .WithMessage($"A workout needs 1 to {WorkoutDefinition.MaxExercises} exercises.");
        RuleForEach(c => c.Exercises).ChildRules(line =>
        {
            line.RuleFor(l => l.ExerciseId).NotEmpty();
            line.RuleFor(l => l.Sets).InclusiveBetween(1, WorkoutDefinition.MaxSets);
            line.RuleFor(l => l.Goal).NotNull();
            line.RuleFor(l => l.Goal!).ChildRules(goal =>
            {
                goal.RuleFor(g => g.Type).NotNull().KebabEnum<GoalRequest, GoalType>();
                goal.RuleFor(g => g.Min)
                    .Must((g, min) => !KebabCase.TryParse(g.Type, out GoalType type) || (min >= 1 && min <= Goal.UpperBound(type)))
                    .WithMessage(g => $"'Min' must be between 1 and {UpperBoundOf(g.Type)}.");
                goal.RuleFor(g => g.Max)
                    .Must((g, max) => !KebabCase.TryParse(g.Type, out GoalType type) || (max >= g.Min && max <= Goal.UpperBound(type)))
                    .WithMessage(g => $"'Max' must be between 'Min' and {UpperBoundOf(g.Type)}.");
            });
            line.RuleFor(l => l.Load).NotNull();
            line.RuleFor(l => l.Load!).ChildRules(load =>
            {
                load.RuleFor(l => l.Type).NotNull().KebabEnum<LoadRequest, LoadType>();
                load.RuleFor(l => l.Value)
                    .Must((l, value) => !KebabCase.TryParse(l.Type, out LoadType type) || Load.Check(type, value) is null)
                    .WithMessage(l => KebabCase.TryParse(l.Type, out LoadType type) ? Load.Check(type, l.Value) : null);
            });
            line.RuleFor(l => l.RestSeconds).InclusiveBetween(0, WorkoutDefinition.MaxRestSeconds);
            line.RuleFor(l => l.Tempo).Matches(TempoPattern).When(l => !string.IsNullOrWhiteSpace(l.Tempo))
                .WithMessage("'Tempo' must look like 3-1-X-0 (four digits or X separated by dashes).");
            line.RuleFor(l => l.SupersetGroup)
                .Must(group => string.IsNullOrWhiteSpace(group) || SupersetGroups.IsLetter(group.Trim()))
                .WithMessage("'Superset Group' must be a single capital letter.");
            line.RuleFor(l => l.Notes).MaximumLength(WorkoutDefinition.NotesMaxLength);
        });
        RuleFor(c => c.Exercises).Custom((lines, context) =>
        {
            foreach ((int index, string message) in SupersetGroups.Check([.. lines.Select(l => Text.NullIfBlank(l.SupersetGroup))]))
            {
                context.AddFailure($"Exercises[{index}].SupersetGroup", message);
            }
        });
    }

    private static int UpperBoundOf(string? type) => KebabCase.TryParse(type, out GoalType parsed) ? Goal.UpperBound(parsed) : Goal.MaxReps;
}

internal sealed record CreateWorkoutCommand(Guid TrainerId, string Name, string? Description, IReadOnlyList<WorkoutExerciseRequest> Exercises)
    : ICommand<WorkoutResponse>, IWorkoutCommand;

internal sealed class CreateWorkoutValidator : WorkoutCommandValidator<CreateWorkoutCommand>;

internal sealed class CreateWorkoutHandler(IWorkoutRepository workouts, IExerciseRepository exercises, TimeProvider timeProvider)
    : ICommandHandler<CreateWorkoutCommand, WorkoutResponse>
{
    public async Task<Result<WorkoutResponse>> Handle(CreateWorkoutCommand command, CancellationToken cancellationToken)
    {
        if (await WorkoutViews.CheckExercisesAsync(command.Exercises, command.TrainerId, new HashSet<Guid>(), exercises, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        var workout = Workout.Create(
            Ids.New(timeProvider),
            command.TrainerId,
            WorkoutViews.ToDefinition(command.Name, command.Description, command.Exercises),
            () => Ids.New(timeProvider),
            timeProvider.UtcNow());

        await workouts.AddAsync(workout, cancellationToken);
        return await WorkoutViews.ToResponseAsync(workout, exercises, cancellationToken);
    }
}

internal sealed record UpdateWorkoutCommand(Guid TrainerId, Guid WorkoutId, string Name, string? Description, IReadOnlyList<WorkoutExerciseRequest> Exercises)
    : ICommand<WorkoutResponse>, IWorkoutCommand;

internal sealed class UpdateWorkoutValidator : WorkoutCommandValidator<UpdateWorkoutCommand>;

internal sealed class UpdateWorkoutHandler(IWorkoutRepository workouts, IExerciseRepository exercises, TimeProvider timeProvider)
    : ICommandHandler<UpdateWorkoutCommand, WorkoutResponse>
{
    public async Task<Result<WorkoutResponse>> Handle(UpdateWorkoutCommand command, CancellationToken cancellationToken)
    {
        Workout? workout = await workouts.GetAsync(command.WorkoutId, command.TrainerId, cancellationToken);

        if (workout is null)
        {
            return WorkoutErrors.NotFound(command.WorkoutId);
        }

        HashSet<Guid> used = [.. workout.Exercises.Select(e => e.ExerciseId)];

        if (await WorkoutViews.CheckExercisesAsync(command.Exercises, command.TrainerId, used, exercises, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        workout.Replace(WorkoutViews.ToDefinition(command.Name, command.Description, command.Exercises), () => Ids.New(timeProvider), timeProvider.UtcNow());
        await workouts.UpdateAsync(workout, cancellationToken);
        return await WorkoutViews.ToResponseAsync(workout, exercises, cancellationToken);
    }
}

internal sealed record DuplicateWorkoutCommand(Guid TrainerId, Guid WorkoutId) : ICommand<WorkoutResponse>;

internal sealed class DuplicateWorkoutHandler(IWorkoutRepository workouts, IExerciseRepository exercises, TimeProvider timeProvider)
    : ICommandHandler<DuplicateWorkoutCommand, WorkoutResponse>
{
    public async Task<Result<WorkoutResponse>> Handle(DuplicateWorkoutCommand command, CancellationToken cancellationToken)
    {
        Workout? workout = await workouts.GetAsync(command.WorkoutId, command.TrainerId, cancellationToken);

        if (workout is null)
        {
            return WorkoutErrors.NotFound(command.WorkoutId);
        }

        Workout copy = workout.Duplicate(Ids.New(timeProvider), () => Ids.New(timeProvider), timeProvider.UtcNow());
        await workouts.AddAsync(copy, cancellationToken);
        return await WorkoutViews.ToResponseAsync(copy, exercises, cancellationToken);
    }
}

internal sealed record DeleteWorkoutCommand(Guid TrainerId, Guid WorkoutId) : ICommand<Unit>;

internal sealed class DeleteWorkoutHandler(IWorkoutRepository workouts, TimeProvider timeProvider) : ICommandHandler<DeleteWorkoutCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteWorkoutCommand command, CancellationToken cancellationToken)
    {
        Workout? workout = await workouts.GetAsync(command.WorkoutId, command.TrainerId, cancellationToken);

        if (workout is null)
        {
            return WorkoutErrors.NotFound(command.WorkoutId);
        }

        workout.Archive(timeProvider.UtcNow());
        await workouts.UpdateAsync(workout, cancellationToken);
        return Result.Success();
    }
}

internal sealed class WorkoutEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/workouts").WithTags("Workouts");

        group.MapGet(
                string.Empty,
                (string? search, bool? includeArchived, string? sort, int? page, int? pageSize, IQueryHandler<ListWorkoutsQuery, Paged<WorkoutSummaryResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(
                        new ListWorkoutsQuery(http.User.GetUserId(), search, includeArchived ?? false, sort, page ?? 1, pageSize ?? PageRequest.DefaultPageSize),
                        cancellationToken).ToOk())
            .WithName("ListWorkouts")
            .RequirePermission(Permissions.WorkoutsManage);

        // Readable by clients too, so they get 404 (not 403) like for any resource that is not theirs.
        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetWorkoutQuery, WorkoutResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetWorkoutQuery(http.User.ToViewer(), id), cancellationToken).ToOk())
            .WithName("GetWorkout")
            .RequirePermission(Permissions.AssignmentsRead);

        group.MapPost(
                string.Empty,
                (WorkoutRequest request, ICommandHandler<CreateWorkoutCommand, WorkoutResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new CreateWorkoutCommand(http.User.GetUserId(), request.Name, request.Description, request.Exercises ?? []), cancellationToken)
                        .ToHttpResult(workout => TypedResults.Created($"/api/workouts/{workout.Id}", workout)))
            .WithName("CreateWorkout")
            .RequirePermission(Permissions.WorkoutsManage);

        group.MapPut(
                "/{id:guid}",
                (Guid id, WorkoutRequest request, ICommandHandler<UpdateWorkoutCommand, WorkoutResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateWorkoutCommand(http.User.GetUserId(), id, request.Name, request.Description, request.Exercises ?? []), cancellationToken).ToOk())
            .WithName("UpdateWorkout")
            .RequirePermission(Permissions.WorkoutsManage);

        group.MapPost(
                "/{id:guid}/duplicate",
                (Guid id, ICommandHandler<DuplicateWorkoutCommand, WorkoutResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DuplicateWorkoutCommand(http.User.GetUserId(), id), cancellationToken)
                        .ToHttpResult(workout => TypedResults.Created($"/api/workouts/{workout.Id}", workout)))
            .WithName("DuplicateWorkout")
            .RequirePermission(Permissions.WorkoutsManage);

        group.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<DeleteWorkoutCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteWorkoutCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("DeleteWorkout")
            .RequirePermission(Permissions.WorkoutsManage);
    }
}
