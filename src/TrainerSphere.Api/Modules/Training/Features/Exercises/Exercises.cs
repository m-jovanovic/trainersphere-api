using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Exercises;

internal interface IExerciseCommand
{
    string Name { get; }

    string Category { get; }

    string MovementPattern { get; }

    IReadOnlyList<string> PrimaryMuscles { get; }

    IReadOnlyList<string> SecondaryMuscles { get; }

    IReadOnlyList<string> Equipment { get; }

    bool Unilateral { get; }

    string? Cue { get; }
}

internal static class ExerciseRules
{
    public static IRuleBuilderOptions<T, string> InTaxonomy<T>(this IRuleBuilder<T, string> rule, IReadOnlyList<string> allowed) =>
        rule.Must(value => allowed.Contains(value, StringComparer.Ordinal))
            .WithMessage($"'{{PropertyName}}' must be one of: {string.Join(", ", allowed)}.");
}

internal sealed record ListExercisesQuery(
    Viewer Viewer,
    string? Search,
    IReadOnlyList<string> Category,
    IReadOnlyList<string> Pattern,
    IReadOnlyList<string> Muscle,
    IReadOnlyList<string> Equipment,
    bool? Custom,
    bool IncludeArchived,
    int Page,
    int PageSize) : IQuery<Paged<ExerciseResponse>>, IPagedQuery;

internal sealed class ListExercisesValidator : PagedQueryValidator<ListExercisesQuery>
{
    public const int MaxPageSize = 200;

    public ListExercisesValidator(ExerciseLibrary library)
        : base(MaxPageSize)
    {
        RuleFor(q => q.Search).MaximumLength(TrainingRules.SearchMaxLength);
        RuleForEach(q => q.Category).InTaxonomy(library.Taxonomy.Categories);
        RuleForEach(q => q.Pattern).InTaxonomy(library.Taxonomy.MovementPatterns);
        RuleForEach(q => q.Muscle).InTaxonomy(library.Taxonomy.Muscles);
        RuleForEach(q => q.Equipment).InTaxonomy(library.Taxonomy.Equipment);
    }
}

internal sealed class ListExercisesHandler(IDbSession db) : IQueryHandler<ListExercisesQuery, Paged<ExerciseResponse>>
{
    private const string Filter =
        """
        (trainer_id IS NULL OR trainer_id = @TrainerScope)
        AND (@IncludeArchived OR NOT archived)
        AND (@Custom::boolean IS NULL OR (trainer_id IS NOT NULL) = @Custom::boolean)
        AND (@Search::text IS NULL OR name ILIKE @Search::text)
        AND (cardinality(@Categories::text[]) = 0 OR category = ANY(@Categories::text[]))
        AND (cardinality(@Patterns::text[]) = 0 OR movement_pattern = ANY(@Patterns::text[]))
        AND (cardinality(@Muscles::text[]) = 0 OR primary_muscles && @Muscles::text[] OR secondary_muscles && @Muscles::text[])
        AND (cardinality(@Equipment::text[]) = 0 OR equipment && @Equipment::text[])
        """;

    public async Task<Result<Paged<ExerciseResponse>>> Handle(ListExercisesQuery query, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            query.Viewer.TrainerScope,
            query.IncludeArchived,
            query.Custom,
            Search = TrainingRules.LikePattern(query.Search),
            Categories = query.Category.ToArray(),
            Patterns = query.Pattern.ToArray(),
            Muscles = query.Muscle.ToArray(),
            Equipment = query.Equipment.ToArray(),
            query.PageSize,
            Offset = (query.Page - 1) * query.PageSize,
        };

        int total = await db.QuerySingleAsync<int>($"SELECT count(*)::int FROM training.exercises WHERE {Filter}", parameters, cancellationToken);
        IReadOnlyList<ExerciseRecord> records = await db.QueryAsync<ExerciseRecord>(
            $"SELECT {ExerciseSql.Columns} FROM training.exercises WHERE {Filter} ORDER BY lower(name), id LIMIT @PageSize OFFSET @Offset",
            parameters,
            cancellationToken);

        return new Paged<ExerciseResponse>([.. records.Select(r => ExerciseResponse.From(Exercise.Restore(r)))], query.Page, query.PageSize, total);
    }
}

internal sealed record GetExerciseTaxonomyQuery : IQuery<ExerciseTaxonomy>;

internal sealed class GetExerciseTaxonomyHandler(ExerciseLibrary library) : IQueryHandler<GetExerciseTaxonomyQuery, ExerciseTaxonomy>
{
    public Task<Result<ExerciseTaxonomy>> Handle(GetExerciseTaxonomyQuery query, CancellationToken cancellationToken) =>
        Task.FromResult<Result<ExerciseTaxonomy>>(library.Taxonomy);
}

internal sealed record GetExerciseQuery(Viewer Viewer, Guid ExerciseId) : IQuery<ExerciseResponse>;

internal sealed class GetExerciseHandler(IExerciseRepository exercises) : IQueryHandler<GetExerciseQuery, ExerciseResponse>
{
    public Task<Result<ExerciseResponse>> Handle(GetExerciseQuery query, CancellationToken cancellationToken) =>
        exercises.GetAsync(query.ExerciseId, cancellationToken)
            .ToResult(ExerciseErrors.NotFound(query.ExerciseId))
            .Ensure(exercise => exercise.IsVisibleTo(query.Viewer), ExerciseErrors.NotFound(query.ExerciseId))
            .Map(ExerciseResponse.From);
}

internal sealed record ExerciseRequest(
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string>? PrimaryMuscles,
    IReadOnlyList<string>? SecondaryMuscles,
    IReadOnlyList<string>? Equipment,
    bool Unilateral,
    string? Cue);

internal sealed record CreateExerciseCommand(
    Guid TrainerId,
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment,
    bool Unilateral,
    string? Cue) : ICommand<ExerciseResponse>, IExerciseCommand
{
    public static CreateExerciseCommand From(Guid trainerId, ExerciseRequest request) =>
        new(
            trainerId,
            request.Name,
            request.Category,
            request.MovementPattern,
            request.PrimaryMuscles ?? [],
            request.SecondaryMuscles ?? [],
            request.Equipment ?? [],
            request.Unilateral,
            request.Cue);
}

internal sealed record UpdateExerciseCommand(
    Guid TrainerId,
    Guid ExerciseId,
    string Name,
    string Category,
    string MovementPattern,
    IReadOnlyList<string> PrimaryMuscles,
    IReadOnlyList<string> SecondaryMuscles,
    IReadOnlyList<string> Equipment,
    bool Unilateral,
    string? Cue) : ICommand<ExerciseResponse>, IExerciseCommand
{
    public static UpdateExerciseCommand From(Guid trainerId, Guid exerciseId, ExerciseRequest request) =>
        new(
            trainerId,
            exerciseId,
            request.Name,
            request.Category,
            request.MovementPattern,
            request.PrimaryMuscles ?? [],
            request.SecondaryMuscles ?? [],
            request.Equipment ?? [],
            request.Unilateral,
            request.Cue);
}

internal static class ExerciseCommands
{
    public static ExerciseDefinition ToDefinition(this IExerciseCommand command) =>
        new(command.Name, command.Category, command.MovementPattern, command.PrimaryMuscles, command.SecondaryMuscles, command.Equipment, command.Unilateral, command.Cue);
}

internal abstract class ExerciseCommandValidator<T> : AbstractValidator<T>
    where T : IExerciseCommand
{
    protected ExerciseCommandValidator(ExerciseLibrary library)
    {
        ExerciseTaxonomy taxonomy = library.Taxonomy;

        RuleFor(c => c.Name).ValidName(ExerciseDefinition.NameMaxLength);
        RuleFor(c => c.Category).NotNull().InTaxonomy(taxonomy.Categories);
        RuleFor(c => c.MovementPattern).NotNull().InTaxonomy(taxonomy.MovementPatterns);
        RuleFor(c => c.PrimaryMuscles).NotEmpty().WithMessage("At least one primary muscle is required.");
        RuleForEach(c => c.PrimaryMuscles).NotNull().InTaxonomy(taxonomy.Muscles);
        RuleForEach(c => c.SecondaryMuscles).NotNull().InTaxonomy(taxonomy.Muscles);
        RuleForEach(c => c.Equipment).NotNull().InTaxonomy(taxonomy.Equipment);
        RuleFor(c => c.Cue).MaximumLength(ExerciseDefinition.CueMaxLength);
    }
}

internal sealed class CreateExerciseValidator(ExerciseLibrary library) : ExerciseCommandValidator<CreateExerciseCommand>(library);

internal sealed class UpdateExerciseValidator(ExerciseLibrary library) : ExerciseCommandValidator<UpdateExerciseCommand>(library);

internal sealed class CreateExerciseHandler(IExerciseRepository exercises, TimeProvider timeProvider) : ICommandHandler<CreateExerciseCommand, ExerciseResponse>
{
    public async Task<Result<ExerciseResponse>> Handle(CreateExerciseCommand command, CancellationToken cancellationToken)
    {
        string name = command.Name.Trim();

        if (await exercises.NameInUseAsync(command.TrainerId, name, exceptId: null, cancellationToken))
        {
            return ExerciseErrors.NameInUse(name);
        }

        var exercise = Exercise.CreateCustom(Ids.New(timeProvider), command.TrainerId, command.ToDefinition(), timeProvider.UtcNow());

        return await exercises.TryAddAsync(exercise, cancellationToken)
            ? ExerciseResponse.From(exercise)
            : ExerciseErrors.NameInUse(name);
    }
}

internal sealed class UpdateExerciseHandler(IExerciseRepository exercises, TimeProvider timeProvider) : ICommandHandler<UpdateExerciseCommand, ExerciseResponse>
{
    public async Task<Result<ExerciseResponse>> Handle(UpdateExerciseCommand command, CancellationToken cancellationToken)
    {
        Result<Exercise> updated = await exercises.GetAsync(command.ExerciseId, cancellationToken)
            .ToResult(ExerciseErrors.NotFound(command.ExerciseId))
            .Ensure(exercise => exercise.IsVisibleTo(Viewer.Trainer(command.TrainerId)), ExerciseErrors.NotFound(command.ExerciseId))
            .Bind(exercise => exercise.Update(command.ToDefinition(), timeProvider.UtcNow()));

        if (updated.Value is not Success<Exercise> { Value: var exercise })
        {
            return updated.Map(ExerciseResponse.From);
        }

        if (await exercises.NameInUseAsync(command.TrainerId, exercise.Name, exercise.Id, cancellationToken) ||
            !await exercises.TryUpdateAsync(exercise, cancellationToken))
        {
            return ExerciseErrors.NameInUse(exercise.Name);
        }

        return ExerciseResponse.From(exercise);
    }
}

internal sealed record DeleteExerciseCommand(Guid TrainerId, Guid ExerciseId) : ICommand<Unit>;

internal sealed class DeleteExerciseHandler(IExerciseRepository exercises, TimeProvider timeProvider) : ICommandHandler<DeleteExerciseCommand, Unit>
{
    public Task<Result<Unit>> Handle(DeleteExerciseCommand command, CancellationToken cancellationToken) =>
        exercises.GetAsync(command.ExerciseId, cancellationToken)
            .ToResult(ExerciseErrors.NotFound(command.ExerciseId))
            .Ensure(exercise => exercise.IsVisibleTo(Viewer.Trainer(command.TrainerId)), ExerciseErrors.NotFound(command.ExerciseId))
            .Bind(exercise => exercise.Archive(timeProvider.UtcNow()))
            .Tap(exercise => exercises.TryUpdateAsync(exercise, cancellationToken))
            .Map(_ => default(Unit));
}

internal sealed class ExerciseEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/exercises").WithTags("Exercises");

        group.MapGet(
                string.Empty,
                (
                    string? search,
                    string[]? category,
                    string[]? pattern,
                    string[]? muscle,
                    string[]? equipment,
                    bool? custom,
                    bool? includeArchived,
                    int? page,
                    int? pageSize,
                    IQueryHandler<ListExercisesQuery, Paged<ExerciseResponse>> handler,
                    HttpContext http,
                    CancellationToken cancellationToken) =>
                    handler.Handle(
                        new ListExercisesQuery(
                            http.User.ToViewer(),
                            search,
                            category ?? [],
                            pattern ?? [],
                            muscle ?? [],
                            equipment ?? [],
                            custom,
                            includeArchived ?? false,
                            page ?? 1,
                            pageSize ?? PageRequest.DefaultPageSize),
                        cancellationToken).ToOk())
            .WithName("ListExercises")
            .RequirePermission(Permissions.ExercisesRead);

        group.MapGet(
                "/taxonomy",
                (IQueryHandler<GetExerciseTaxonomyQuery, ExerciseTaxonomy> handler, CancellationToken cancellationToken) =>
                    handler.Handle(new GetExerciseTaxonomyQuery(), cancellationToken).ToOk())
            .WithName("GetExerciseTaxonomy")
            .RequirePermission(Permissions.ExercisesRead);

        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetExerciseQuery, ExerciseResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetExerciseQuery(http.User.ToViewer(), id), cancellationToken).ToOk())
            .WithName("GetExercise")
            .RequirePermission(Permissions.ExercisesRead);

        group.MapPost(
                string.Empty,
                (ExerciseRequest request, ICommandHandler<CreateExerciseCommand, ExerciseResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(CreateExerciseCommand.From(http.User.GetUserId(), request), cancellationToken)
                        .ToHttpResult(exercise => TypedResults.Created($"/api/exercises/{exercise.Id}", exercise)))
            .WithName("CreateExercise")
            .RequirePermission(Permissions.ExercisesManage);

        group.MapPut(
                "/{id:guid}",
                (Guid id, ExerciseRequest request, ICommandHandler<UpdateExerciseCommand, ExerciseResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(UpdateExerciseCommand.From(http.User.GetUserId(), id, request), cancellationToken).ToOk())
            .WithName("UpdateExercise")
            .RequirePermission(Permissions.ExercisesManage);

        group.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<DeleteExerciseCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteExerciseCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("DeleteExercise")
            .RequirePermission(Permissions.ExercisesManage);
    }
}
