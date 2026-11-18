using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Programs;

internal interface IProgramCommand
{
    string Name { get; }

    string? Description { get; }

    int Weeks { get; }

    IReadOnlyList<ProgramSlotRequest> Slots { get; }
}

internal sealed record ProgramSummaryResponse(Guid Id, string Name, string? Description, int Weeks, int WorkoutCount, bool Archived, DateTime UpdatedAt);

internal sealed record ProgramSlotRequest(int Week, int Day, Guid WorkoutId);

internal sealed record ProgramRequest(string Name, string? Description, int Weeks, IReadOnlyList<ProgramSlotRequest>? Slots);

internal sealed record AssignProgramRequest(IReadOnlyList<Guid>? ClientIds, DateOnly? StartDate);

internal static class ProgramViews
{
    public static async Task<ProgramResponse> ToResponseAsync(TrainingProgram program, IWorkoutRepository workouts, CancellationToken cancellationToken)
    {
        IReadOnlyList<Workout> found = await workouts.GetManyAsync([.. program.Slots.Select(s => s.WorkoutId)], program.TrainerId, cancellationToken);
        return ProgramResponse.From(program, found.ToDictionary(w => w.Id, w => w.Name));
    }

    /// <summary>
    /// Checks that every slot references one of the trainer's workouts. Archived workouts the program already
    /// uses stay allowed so existing programs remain editable.
    /// </summary>
    public static async Task<ValidationError?> CheckWorkoutsAsync(
        IReadOnlyList<ProgramSlotRequest> slots,
        Guid trainerId,
        IReadOnlySet<Guid> alreadyUsed,
        IWorkoutRepository workouts,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workout> found = await workouts.GetManyAsync([.. slots.Select(s => s.WorkoutId)], trainerId, cancellationToken);
        var byId = found.ToDictionary(w => w.Id);
        List<FieldError> errors = [];

        for (int index = 0; index < slots.Count; index++)
        {
            Guid workoutId = slots[index].WorkoutId;
            string field = $"slots[{index}].workoutId";

            if (!byId.TryGetValue(workoutId, out Workout? workout))
            {
                NotFoundError error = WorkoutErrors.NotFound(workoutId);
                errors.Add(new FieldError(field, error.Code, error.Message));
            }
            else if (workout.Archived && !alreadyUsed.Contains(workoutId))
            {
                DomainError error = WorkoutErrors.Archived(workoutId);
                errors.Add(new FieldError(field, error.Code, error.Message));
            }
        }

        return errors.Count == 0 ? null : new ValidationError(errors);
    }

    public static ProgramDefinition ToDefinition(this IProgramCommand command) =>
        new(command.Name, command.Description, command.Weeks, [.. command.Slots.Select(s => new ProgramSlotDefinition(s.Week, s.Day, s.WorkoutId))]);
}

internal sealed record ListProgramsQuery(Guid TrainerId, string? Search, bool IncludeArchived, int Page, int PageSize)
    : IQuery<Paged<ProgramSummaryResponse>>, IPagedQuery;

internal sealed class ListProgramsValidator : PagedQueryValidator<ListProgramsQuery>
{
    public ListProgramsValidator() => RuleFor(q => q.Search).MaximumLength(TrainingRules.SearchMaxLength);
}

internal sealed class ListProgramsHandler(IDbSession db) : IQueryHandler<ListProgramsQuery, Paged<ProgramSummaryResponse>>
{
    private const string Filter =
        "p.trainer_id = @TrainerId AND (@IncludeArchived OR NOT p.archived) AND (@Search::text IS NULL OR p.name ILIKE @Search::text)";

    public async Task<Result<Paged<ProgramSummaryResponse>>> Handle(ListProgramsQuery query, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            query.TrainerId,
            query.IncludeArchived,
            Search = TrainingRules.LikePattern(query.Search),
            query.PageSize,
            Offset = (query.Page - 1) * query.PageSize,
        };

        int total = await db.QuerySingleAsync<int>($"SELECT count(*)::int FROM training.programs p WHERE {Filter}", parameters, cancellationToken);
        IReadOnlyList<ProgramSummaryRow> rows = await db.QueryAsync<ProgramSummaryRow>(
            $"""
            SELECT p.id, p.name, p.description, p.weeks, p.archived, p.updated_at,
                   (SELECT count(*)::int FROM training.program_slots s WHERE s.program_id = p.id) AS workout_count
            FROM training.programs p
            WHERE {Filter}
            ORDER BY lower(p.name), p.id
            LIMIT @PageSize OFFSET @Offset
            """,
            parameters,
            cancellationToken);

        return new Paged<ProgramSummaryResponse>(
            [.. rows.Select(r => new ProgramSummaryResponse(r.Id, r.Name, r.Description, r.Weeks, r.WorkoutCount, r.Archived, r.UpdatedAt))],
            query.Page,
            query.PageSize,
            total);
    }

    internal sealed record ProgramSummaryRow
    {
        public Guid Id { get; init; }

        public string Name { get; init; } = string.Empty;

        public string? Description { get; init; }

        public int Weeks { get; init; }

        public int WorkoutCount { get; init; }

        public bool Archived { get; init; }

        public DateTime UpdatedAt { get; init; }
    }
}

internal sealed record GetProgramQuery(Guid TrainerId, Guid ProgramId) : IQuery<ProgramResponse>;

internal sealed class GetProgramHandler(IProgramRepository programs, IWorkoutRepository workouts) : IQueryHandler<GetProgramQuery, ProgramResponse>
{
    public async Task<Result<ProgramResponse>> Handle(GetProgramQuery query, CancellationToken cancellationToken)
    {
        TrainingProgram? program = await programs.GetAsync(query.ProgramId, query.TrainerId, cancellationToken);

        return program is null
            ? ProgramErrors.NotFound(query.ProgramId)
            : await ProgramViews.ToResponseAsync(program, workouts, cancellationToken);
    }
}

internal abstract class ProgramCommandValidator<T> : AbstractValidator<T>
    where T : IProgramCommand
{
    protected ProgramCommandValidator()
    {
        RuleFor(c => c.Name).ValidName(ProgramDefinition.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(ProgramDefinition.DescriptionMaxLength);
        RuleFor(c => c.Weeks).InclusiveBetween(1, TrainingProgram.MaxWeeks);
        RuleFor(c => c.Slots)
            .Must(slots => slots.Count <= TrainingProgram.MaxWeeks * TrainingProgram.DaysPerWeek * TrainingProgram.MaxSlotsPerDay)
            .WithMessage("The program has too many slots.");
        RuleForEach(c => c.Slots).ChildRules(slot => slot.RuleFor(s => s.WorkoutId).NotEmpty());
    }
}

internal sealed record CreateProgramCommand(Guid TrainerId, string Name, string? Description, int Weeks, IReadOnlyList<ProgramSlotRequest> Slots)
    : ICommand<ProgramResponse>, IProgramCommand;

internal sealed class CreateProgramValidator : ProgramCommandValidator<CreateProgramCommand>;

internal sealed class CreateProgramHandler(IProgramRepository programs, IWorkoutRepository workouts, TimeProvider timeProvider)
    : ICommandHandler<CreateProgramCommand, ProgramResponse>
{
    public async Task<Result<ProgramResponse>> Handle(CreateProgramCommand command, CancellationToken cancellationToken)
    {
        if (await ProgramViews.CheckWorkoutsAsync(command.Slots, command.TrainerId, new HashSet<Guid>(), workouts, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return await TrainingProgram.Create(Ids.New(timeProvider), command.TrainerId, command.ToDefinition(), () => Ids.New(timeProvider), timeProvider.UtcNow())
            .Tap(program => programs.AddAsync(program, cancellationToken))
            .Map(program => ProgramViews.ToResponseAsync(program, workouts, cancellationToken));
    }
}

internal sealed record UpdateProgramCommand(Guid TrainerId, Guid ProgramId, string Name, string? Description, int Weeks, IReadOnlyList<ProgramSlotRequest> Slots)
    : ICommand<ProgramResponse>, IProgramCommand;

internal sealed class UpdateProgramValidator : ProgramCommandValidator<UpdateProgramCommand>;

internal sealed class UpdateProgramHandler(IProgramRepository programs, IWorkoutRepository workouts, TimeProvider timeProvider)
    : ICommandHandler<UpdateProgramCommand, ProgramResponse>
{
    public async Task<Result<ProgramResponse>> Handle(UpdateProgramCommand command, CancellationToken cancellationToken)
    {
        TrainingProgram? program = await programs.GetAsync(command.ProgramId, command.TrainerId, cancellationToken);

        if (program is null)
        {
            return ProgramErrors.NotFound(command.ProgramId);
        }

        HashSet<Guid> used = [.. program.Slots.Select(s => s.WorkoutId)];

        if (await ProgramViews.CheckWorkoutsAsync(command.Slots, command.TrainerId, used, workouts, cancellationToken) is { } invalid)
        {
            return invalid;
        }

        return await program.Replace(command.ToDefinition(), () => Ids.New(timeProvider), timeProvider.UtcNow())
            .Tap(replaced => programs.UpdateAsync(replaced, cancellationToken))
            .Map(replaced => ProgramViews.ToResponseAsync(replaced, workouts, cancellationToken));
    }
}

internal sealed record DeleteProgramCommand(Guid TrainerId, Guid ProgramId) : ICommand<Unit>;

internal sealed class DeleteProgramHandler(IProgramRepository programs, TimeProvider timeProvider) : ICommandHandler<DeleteProgramCommand, Unit>
{
    public async Task<Result<Unit>> Handle(DeleteProgramCommand command, CancellationToken cancellationToken)
    {
        TrainingProgram? program = await programs.GetAsync(command.ProgramId, command.TrainerId, cancellationToken);

        if (program is null)
        {
            return ProgramErrors.NotFound(command.ProgramId);
        }

        program.Archive(timeProvider.UtcNow());
        await programs.UpdateAsync(program, cancellationToken);
        return Result.Success();
    }
}

internal sealed record AssignProgramCommand(Guid TrainerId, Guid ProgramId, IReadOnlyList<Guid> ClientIds, DateOnly? StartDate)
    : ICommand<IReadOnlyList<ProgramAssignmentResponse>>;

internal sealed class AssignProgramValidator : AbstractValidator<AssignProgramCommand>
{
    public const int MaxClients = 50;

    public AssignProgramValidator()
    {
        RuleFor(c => c.ClientIds)
            .Must(ids => ids.Count is >= 1 and <= MaxClients)
            .WithMessage($"Choose 1 to {MaxClients} clients.");
        RuleForEach(c => c.ClientIds).NotEmpty();
        RuleFor(c => c.StartDate).NotNull();
    }
}

internal sealed class AssignProgramHandler(
    IProgramRepository programs,
    IWorkoutRepository workouts,
    IExerciseRepository exercises,
    IClientRepository clients,
    IAssignmentRepository assignments,
    TimeProvider timeProvider) : ICommandHandler<AssignProgramCommand, IReadOnlyList<ProgramAssignmentResponse>>
{
    public Task<Result<IReadOnlyList<ProgramAssignmentResponse>>> Handle(AssignProgramCommand command, CancellationToken cancellationToken) =>
        programs.GetAsync(command.ProgramId, command.TrainerId, cancellationToken)
            .ToResult(ProgramErrors.NotFound(command.ProgramId))
            .Bind(program => program.EnsureActive())
            .Bind(program => LoadClientsAsync(command, cancellationToken).Map(targets => (Program: program, Clients: targets)))
            .Map(target => ScheduleAsync(target.Program, target.Clients, command.StartDate!.Value, cancellationToken));

    private async Task<IReadOnlyList<ProgramAssignmentResponse>> ScheduleAsync(
        TrainingProgram program,
        IReadOnlyList<ClientProfile> targets,
        DateOnly start,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Workout> found = await workouts.GetManyAsync([.. program.Slots.Select(s => s.WorkoutId)], program.TrainerId, cancellationToken);
        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(found.SelectMany(w => w.Exercises.Select(e => e.ExerciseId)), cancellationToken);
        var snapshots = found.ToDictionary(w => w.Id, w => w.Snapshot(summaries));
        DateTime now = timeProvider.UtcNow();
        List<ProgramAssignmentResponse> created = [];

        foreach (ClientProfile client in targets)
        {
            ProgramAssignment assignment = new(Ids.New(timeProvider), program.Id, program.Name, program.TrainerId, client.Id, start, TrainingProgram.EndDate(start, program.Weeks), now);
            await programs.AddAssignmentAsync(assignment, cancellationToken);

            foreach ((ProgramSlot slot, DateOnly date) in program.Schedule(start))
            {
                await assignments.AddAsync(
                    Assignment.Schedule(Ids.New(timeProvider), client, slot.WorkoutId, snapshots[slot.WorkoutId], date, notes: null, assignment, now),
                    cancellationToken);
            }

            created.Add(new ProgramAssignmentResponse(
                assignment.Id,
                assignment.ProgramId,
                assignment.ProgramName,
                assignment.ClientId,
                assignment.StartDate,
                assignment.EndDate,
                program.Slots.Count,
                assignment.CreatedAt));
        }

        return created;
    }

    private async Task<Result<IReadOnlyList<ClientProfile>>> LoadClientsAsync(AssignProgramCommand command, CancellationToken cancellationToken)
    {
        Guid[] ids = [.. command.ClientIds.Distinct()];
        var byId = (await clients.GetManyAsync(ids, cancellationToken)).ToDictionary(c => c.Id);
        var trainer = Viewer.Trainer(command.TrainerId);
        List<ClientProfile> result = [];

        foreach (Guid id in ids)
        {
            if (!byId.TryGetValue(id, out ClientProfile? client) || !client.IsAccessibleBy(trainer))
            {
                return ClientErrors.NotFound(id);
            }

            if (client.IsArchived)
            {
                return ClientErrors.Archived(id);
            }

            result.Add(client);
        }

        return result;
    }
}

internal sealed record ListProgramAssignmentsQuery(Guid TrainerId, Guid ClientId) : IQuery<IReadOnlyList<ProgramAssignmentResponse>>;

internal sealed class ListProgramAssignmentsHandler(IClientRepository clients, IDbSession db)
    : IQueryHandler<ListProgramAssignmentsQuery, IReadOnlyList<ProgramAssignmentResponse>>
{
    public async Task<Result<IReadOnlyList<ProgramAssignmentResponse>>> Handle(ListProgramAssignmentsQuery query, CancellationToken cancellationToken)
    {
        if (await clients.GetAccessibleAsync(Viewer.Trainer(query.TrainerId), query.ClientId, cancellationToken) is { Value: Error error })
        {
            return error;
        }

        IReadOnlyList<ProgramAssignmentListRow> rows = await db.QueryAsync<ProgramAssignmentListRow>(
            """
            SELECT pa.id, pa.program_id, pa.program_name, pa.client_id, pa.start_date, pa.end_date, pa.created_at,
                   (SELECT count(*)::int FROM training.assignments a WHERE a.program_assignment_id = pa.id) AS assignment_count
            FROM training.program_assignments pa
            WHERE pa.client_id = @ClientId AND pa.trainer_id = @TrainerId AND pa.removed_at IS NULL
            ORDER BY pa.start_date DESC, pa.created_at DESC
            """,
            new { query.ClientId, query.TrainerId },
            cancellationToken);

        return rows.Select(r => new ProgramAssignmentResponse(r.Id, r.ProgramId, r.ProgramName, r.ClientId, r.StartDate, r.EndDate, r.AssignmentCount, r.CreatedAt)).ToList();
    }

    internal sealed record ProgramAssignmentListRow
    {
        public Guid Id { get; init; }

        public Guid ProgramId { get; init; }

        public string ProgramName { get; init; } = string.Empty;

        public Guid ClientId { get; init; }

        public DateOnly StartDate { get; init; }

        public DateOnly EndDate { get; init; }

        public int AssignmentCount { get; init; }

        public DateTime CreatedAt { get; init; }
    }
}

internal sealed record RemoveProgramAssignmentCommand(Guid TrainerId, Guid ProgramAssignmentId) : ICommand<Unit>;

internal sealed class RemoveProgramAssignmentHandler(
    IProgramRepository programs,
    IClientRepository clients,
    IAssignmentRepository assignments,
    TimeProvider timeProvider) : ICommandHandler<RemoveProgramAssignmentCommand, Unit>
{
    public async Task<Result<Unit>> Handle(RemoveProgramAssignmentCommand command, CancellationToken cancellationToken)
    {
        ProgramAssignment? assignment = await programs.GetAssignmentAsync(command.ProgramAssignmentId, command.TrainerId, cancellationToken);

        if (assignment is null)
        {
            return ProgramErrors.AssignmentNotFound(command.ProgramAssignmentId);
        }

        ClientProfile? client = await clients.GetAsync(assignment.ClientId, cancellationToken);
        DateOnly today = timeProvider.Today(client?.TimeZone);

        await assignments.DeleteScheduledForProgramFromAsync(assignment.Id, today, cancellationToken);
        await programs.RemoveAssignmentAsync(assignment.Id, timeProvider.UtcNow(), cancellationToken);
        return Result.Success();
    }
}

internal sealed class ProgramEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/programs").WithTags("Programs").RequirePermission(Permissions.ProgramsManage);

        group.MapGet(
                string.Empty,
                (string? search, bool? includeArchived, int? page, int? pageSize, IQueryHandler<ListProgramsQuery, Paged<ProgramSummaryResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListProgramsQuery(http.User.GetUserId(), search, includeArchived ?? false, page ?? 1, pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ToOk())
            .WithName("ListPrograms");

        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetProgramQuery, ProgramResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetProgramQuery(http.User.GetUserId(), id), cancellationToken).ToOk())
            .WithName("GetProgram");

        group.MapPost(
                string.Empty,
                (ProgramRequest request, ICommandHandler<CreateProgramCommand, ProgramResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new CreateProgramCommand(http.User.GetUserId(), request.Name, request.Description, request.Weeks, request.Slots ?? []), cancellationToken)
                        .ToHttpResult(program => TypedResults.Created($"/api/programs/{program.Id}", program)))
            .WithName("CreateProgram");

        group.MapPut(
                "/{id:guid}",
                (Guid id, ProgramRequest request, ICommandHandler<UpdateProgramCommand, ProgramResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateProgramCommand(http.User.GetUserId(), id, request.Name, request.Description, request.Weeks, request.Slots ?? []), cancellationToken).ToOk())
            .WithName("UpdateProgram");

        group.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<DeleteProgramCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteProgramCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("DeleteProgram");

        group.MapPost(
                "/{id:guid}/assign",
                (Guid id, AssignProgramRequest request, ICommandHandler<AssignProgramCommand, IReadOnlyList<ProgramAssignmentResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new AssignProgramCommand(http.User.GetUserId(), id, request.ClientIds ?? [], request.StartDate), cancellationToken)
                        .ToHttpResult(created => TypedResults.Created((string?)null, created)))
            .WithName("AssignProgram");

        app.MapGet(
                "/api/clients/{clientId:guid}/program-assignments",
                (Guid clientId, IQueryHandler<ListProgramAssignmentsQuery, IReadOnlyList<ProgramAssignmentResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListProgramAssignmentsQuery(http.User.GetUserId(), clientId), cancellationToken).ToOk())
            .WithTags("Programs")
            .WithName("ListProgramAssignments")
            .RequirePermission(Permissions.ProgramsManage);

        app.MapDelete(
                "/api/program-assignments/{id:guid}",
                (Guid id, ICommandHandler<RemoveProgramAssignmentCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new RemoveProgramAssignmentCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithTags("Programs")
            .WithName("RemoveProgramAssignment")
            .RequirePermission(Permissions.ProgramsManage);
    }
}
