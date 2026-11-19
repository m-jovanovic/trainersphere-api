using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Assignments;

internal sealed record CreateAssignmentRequest(Guid ClientId, Guid WorkoutId, DateOnly? Date, string? Notes);

internal sealed record UpdateAssignmentRequest(DateOnly? Date, string? Notes);

internal sealed record SkipAssignmentRequest(string? Reason);

internal static class AssignmentViews
{
    public static async Task<AssignmentResponse> ToResponseAsync(Assignment assignment, IClientRepository clients, TimeProvider timeProvider, CancellationToken cancellationToken)
    {
        ClientProfile client = await clients.GetAsync(assignment.ClientId, cancellationToken)
            ?? throw new InvalidOperationException($"The client '{assignment.ClientId}' of assignment '{assignment.Id}' is missing.");

        return AssignmentResponse.From(assignment, client, timeProvider.Today(client.TimeZone));
    }

    public static Task<Result<Assignment>> GetAccessibleAsync(this IAssignmentRepository assignments, Viewer viewer, Guid id, CancellationToken cancellationToken) =>
        assignments.GetAsync(id, forUpdate: false, cancellationToken)
            .ToResult(AssignmentErrors.NotFound(id))
            .Ensure(assignment => assignment.IsAccessibleBy(viewer), AssignmentErrors.NotFound(id));
}

internal sealed record ListAssignmentsQuery(Viewer Viewer, Guid? ClientId, DateOnly? From, DateOnly? To, string? Status) : IQuery<IReadOnlyList<AssignmentResponse>>;

internal sealed class ListAssignmentsValidator : AbstractValidator<ListAssignmentsQuery>
{
    public ListAssignmentsValidator() => RuleFor(q => q.Status).KebabEnum<ListAssignmentsQuery, AssignmentStatus>();
}

internal sealed class ListAssignmentsHandler(IAssignmentRepository assignments, IClientRepository clients, TimeProvider timeProvider)
    : IQueryHandler<ListAssignmentsQuery, IReadOnlyList<AssignmentResponse>>
{
    public const int DefaultDaysBack = 7;
    public const int DefaultDaysAhead = 28;

    public Task<Result<IReadOnlyList<AssignmentResponse>>> Handle(ListAssignmentsQuery query, CancellationToken cancellationToken) =>
        clients.ScopeAsync(query.Viewer, query.ClientId, cancellationToken)
            .Bind(target => ListAsync(query, target, cancellationToken));

    private async Task<Result<IReadOnlyList<AssignmentResponse>>> ListAsync(ListAssignmentsQuery query, ListScope target, CancellationToken cancellationToken)
    {
        DateOnly today = timeProvider.Today(await clients.TimeZoneOfAsync(query.Viewer, cancellationToken));
        DateOnly from = query.From ?? today.AddDays(-DefaultDaysBack);
        DateOnly to = query.To ?? today.AddDays(DefaultDaysAhead);

        if (to < from)
        {
            return ValidationError.For("to", "Must not be before 'from'.");
        }

        if (to.DayNumber - from.DayNumber + 1 > TrainingRules.MaxAssignmentRangeDays)
        {
            return ValidationError.For("to", $"The date range can span at most {TrainingRules.MaxAssignmentRangeDays} days.");
        }

        IReadOnlyList<Assignment> found = await assignments.ListAsync(target.TrainerId, target.ClientId, from, to, cancellationToken);
        var owners = (await clients.GetManyAsync([.. found.Select(a => a.ClientId).Distinct()], cancellationToken)).ToDictionary(c => c.Id);
        AssignmentStatus? status = KebabCase.TryParse(query.Status, out AssignmentStatus parsed) ? parsed : null;

        return found
            .Select(a => AssignmentResponse.From(a, owners[a.ClientId], timeProvider.Today(owners[a.ClientId].TimeZone)))
            .Where(a => status is null || a.Status == status)
            .ToList();
    }
}

internal sealed record GetAssignmentQuery(Viewer Viewer, Guid AssignmentId) : IQuery<AssignmentResponse>;

internal sealed class GetAssignmentHandler(IAssignmentRepository assignments, IClientRepository clients, TimeProvider timeProvider)
    : IQueryHandler<GetAssignmentQuery, AssignmentResponse>
{
    public Task<Result<AssignmentResponse>> Handle(GetAssignmentQuery query, CancellationToken cancellationToken) =>
        assignments.GetAccessibleAsync(query.Viewer, query.AssignmentId, cancellationToken)
            .Map(assignment => AssignmentViews.ToResponseAsync(assignment, clients, timeProvider, cancellationToken));
}

internal sealed record CreateAssignmentCommand(Guid TrainerId, Guid ClientId, Guid WorkoutId, DateOnly? Date, string? Notes) : ICommand<AssignmentResponse>;

internal sealed class CreateAssignmentValidator : AbstractValidator<CreateAssignmentCommand>
{
    public CreateAssignmentValidator()
    {
        RuleFor(c => c.ClientId).NotEmpty();
        RuleFor(c => c.WorkoutId).NotEmpty();
        RuleFor(c => c.Date).NotNull();
        RuleFor(c => c.Notes).MaximumLength(Assignment.NotesMaxLength);
    }
}

internal sealed class CreateAssignmentHandler(
    IAssignmentRepository assignments,
    IClientRepository clients,
    IWorkoutRepository workouts,
    IExerciseRepository exercises,
    TimeProvider timeProvider) : ICommandHandler<CreateAssignmentCommand, AssignmentResponse>
{
    public Task<Result<AssignmentResponse>> Handle(CreateAssignmentCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(Viewer.Trainer(command.TrainerId), command.ClientId, cancellationToken)
            .Bind(client => client.EnsureActive())
            .Bind(client => ScheduleAsync(command, client, cancellationToken));

    private async Task<Result<AssignmentResponse>> ScheduleAsync(CreateAssignmentCommand command, ClientProfile target, CancellationToken cancellationToken)
    {
        Workout? workout = await workouts.GetAsync(command.WorkoutId, command.TrainerId, cancellationToken);

        if (workout is null)
        {
            return WorkoutErrors.NotFound(command.WorkoutId);
        }

        if (workout.Archived)
        {
            return WorkoutErrors.Archived(workout.Id);
        }

        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(workout.Exercises.Select(e => e.ExerciseId), cancellationToken);
        var assignment = Assignment.Schedule(
            Ids.New(timeProvider),
            target,
            workout.Id,
            workout.Snapshot(summaries),
            command.Date!.Value,
            command.Notes,
            program: null,
            timeProvider.UtcNow());

        await assignments.AddAsync(assignment, cancellationToken);
        return AssignmentResponse.From(assignment, target, timeProvider.Today(target.TimeZone));
    }
}

internal sealed record UpdateAssignmentCommand(Guid TrainerId, Guid AssignmentId, DateOnly? Date, string? Notes) : ICommand<AssignmentResponse>;

internal sealed class UpdateAssignmentValidator : AbstractValidator<UpdateAssignmentCommand>
{
    public UpdateAssignmentValidator()
    {
        RuleFor(c => c.Date).NotNull();
        RuleFor(c => c.Notes).MaximumLength(Assignment.NotesMaxLength);
    }
}

internal sealed class UpdateAssignmentHandler(IAssignmentRepository assignments, IClientRepository clients, TimeProvider timeProvider)
    : ICommandHandler<UpdateAssignmentCommand, AssignmentResponse>
{
    public Task<Result<AssignmentResponse>> Handle(UpdateAssignmentCommand command, CancellationToken cancellationToken) =>
        assignments.GetAccessibleAsync(Viewer.Trainer(command.TrainerId), command.AssignmentId, cancellationToken)
            .Bind(assignment => assignment.Reschedule(command.Date!.Value, command.Notes, timeProvider.UtcNow()))
            .Tap(assignment => assignments.UpdateAsync(assignment, cancellationToken))
            .Map(assignment => AssignmentViews.ToResponseAsync(assignment, clients, timeProvider, cancellationToken));
}

internal sealed record DeleteAssignmentCommand(Guid TrainerId, Guid AssignmentId) : ICommand<Unit>;

internal sealed class DeleteAssignmentHandler(IAssignmentRepository assignments) : ICommandHandler<DeleteAssignmentCommand, Unit>
{
    public Task<Result<Unit>> Handle(DeleteAssignmentCommand command, CancellationToken cancellationToken) =>
        assignments.GetAccessibleAsync(Viewer.Trainer(command.TrainerId), command.AssignmentId, cancellationToken)
            .Bind(assignment => assignment.EnsureDeletable())
            .Tap(assignment => assignments.DeleteAsync(assignment.Id, cancellationToken))
            .Map(_ => default(Unit));
}

internal sealed record SkipAssignmentCommand(Viewer Viewer, Guid AssignmentId, string? Reason) : ICommand<AssignmentResponse>;

internal sealed class SkipAssignmentValidator : AbstractValidator<SkipAssignmentCommand>
{
    public SkipAssignmentValidator() => RuleFor(c => c.Reason).MaximumLength(Assignment.ReasonMaxLength);
}

internal sealed class SkipAssignmentHandler(IAssignmentRepository assignments, IClientRepository clients, TimeProvider timeProvider)
    : ICommandHandler<SkipAssignmentCommand, AssignmentResponse>
{
    public Task<Result<AssignmentResponse>> Handle(SkipAssignmentCommand command, CancellationToken cancellationToken) =>
        assignments.GetAccessibleAsync(command.Viewer, command.AssignmentId, cancellationToken)
            .Bind(assignment => assignment.Skip(command.Reason, timeProvider.UtcNow()))
            .Tap(assignment => assignments.UpdateAsync(assignment, cancellationToken))
            .Map(assignment => AssignmentViews.ToResponseAsync(assignment, clients, timeProvider, cancellationToken));
}

internal sealed class AssignmentEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/assignments").WithTags("Assignments");

        group.MapGet(
                string.Empty,
                (Guid? clientId, DateOnly? from, DateOnly? to, string? status, IQueryHandler<ListAssignmentsQuery, IReadOnlyList<AssignmentResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListAssignmentsQuery(http.User.ToViewer(), clientId, from, to, status), cancellationToken).ToOk())
            .WithName("ListAssignments")
            .RequirePermission(Permissions.AssignmentsRead);

        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetAssignmentQuery, AssignmentResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetAssignmentQuery(http.User.ToViewer(), id), cancellationToken).ToOk())
            .WithName("GetAssignment")
            .RequirePermission(Permissions.AssignmentsRead);

        group.MapPost(
                string.Empty,
                (CreateAssignmentRequest request, ICommandHandler<CreateAssignmentCommand, AssignmentResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new CreateAssignmentCommand(http.User.GetUserId(), request.ClientId, request.WorkoutId, request.Date, request.Notes), cancellationToken)
                        .ToHttpResult(assignment => TypedResults.Created($"/api/assignments/{assignment.Id}", assignment)))
            .WithName("CreateAssignment")
            .RequirePermission(Permissions.AssignmentsManage);

        group.MapPut(
                "/{id:guid}",
                (Guid id, UpdateAssignmentRequest request, ICommandHandler<UpdateAssignmentCommand, AssignmentResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateAssignmentCommand(http.User.GetUserId(), id, request.Date, request.Notes), cancellationToken).ToOk())
            .WithName("UpdateAssignment")
            .RequirePermission(Permissions.AssignmentsManage);

        group.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<DeleteAssignmentCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteAssignmentCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("DeleteAssignment")
            .RequirePermission(Permissions.AssignmentsManage);

        group.MapPost(
                "/{id:guid}/skip",
                (Guid id, SkipAssignmentRequest? request, ICommandHandler<SkipAssignmentCommand, AssignmentResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new SkipAssignmentCommand(http.User.ToViewer(), id, request?.Reason), cancellationToken).ToOk())
            .WithName("SkipAssignment")
            .RequirePermission(Permissions.AssignmentsSkip);
    }
}
