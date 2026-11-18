using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Sessions;

internal sealed record StartSessionRequest(Guid? Id, Guid? AssignmentId, string? WorkoutName, DateTime? StartedAt);

internal sealed record UpdateSessionRequest(string WorkoutName, string? Notes);

internal sealed record SetRequest(
    Guid ExerciseId,
    int Position,
    int SetNumber,
    decimal? WeightKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters,
    decimal? Rpe,
    bool Completed,
    string? Notes);

internal sealed record CompleteSessionRequest(DateTime? CompletedAt, string? Notes);

/// <summary>
/// The outcome of starting a session: <see cref="Created"/> is false when the id already existed for the client.
/// </summary>
internal sealed record StartedSession(SessionResponse Session, bool Created);

internal static class SessionViews
{
    public static Task<Result<Session>> GetAccessibleAsync(this ISessionRepository sessions, Viewer viewer, Guid id, bool forUpdate, CancellationToken cancellationToken) =>
        sessions.GetAsync(id, forUpdate, cancellationToken)
            .ToResult(SessionErrors.NotFound(id))
            .Ensure(session => session.IsAccessibleBy(viewer), SessionErrors.NotFound(id));

    /// <summary>
    /// Builds the session view: the prescription merged with the logged sets by position, last performance per
    /// exercise, totals and the personal records set by the session.
    /// </summary>
    public static async Task<SessionResponse> ToResponseAsync(
        Session session,
        IClientRepository clients,
        ISessionRepository sessions,
        IExerciseRepository exercises,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<SessionSet> sets = await sessions.GetSetsAsync(session.Id, cancellationToken);
        IReadOnlyList<PersonalRecord> records = await sessions.GetPersonalRecordsAsync(session.Id, cancellationToken);
        ClientProfile? client = await clients.GetAsync(session.ClientId, cancellationToken);
        TimeZoneInfo zone = TimeZones.FindOrUtc(client?.TimeZone);

        var prescribed = session.Prescription.ToDictionary(line => line.Position);
        ILookup<int, SessionSet> logged = sets.ToLookup(set => set.Position);
        int[] positions = [.. prescribed.Keys.Union(logged.Select(group => group.Key)).Order()];
        Guid ExerciseAt(int position) => prescribed.TryGetValue(position, out WorkoutExerciseView? line) ? line.ExerciseId : logged[position].First().ExerciseId;
        Guid[] exerciseIds = [.. positions.Select(ExerciseAt).Distinct()];

        var known = session.Prescription.Select(line => line.Exercise).DistinctBy(e => e.Id).ToDictionary(e => e.Id);
        IReadOnlyDictionary<Guid, ExerciseSummary> loaded = await exercises.GetSummariesAsync(
            exerciseIds.Concat(records.Select(r => r.ExerciseId)).Where(id => !known.ContainsKey(id)),
            cancellationToken);
        ExerciseSummary SummaryOf(Guid id) =>
            known.GetValueOrDefault(id) ?? loaded.GetValueOrDefault(id) ?? new ExerciseSummary(id, null, string.Empty, null);

        ILookup<Guid, PerformedSet> last = (await sessions.GetLastPerformanceAsync(session.ClientId, session.Id, exerciseIds, cancellationToken)).ToLookup(s => s.ExerciseId);
        LastPerformance? LastOf(Guid exerciseId) =>
            last[exerciseId].FirstOrDefault() is { } first
                ? new LastPerformance(first.SessionId, LocalDates.Of(first.StartedAt, zone), [.. last[exerciseId].Select(s => new LastPerformanceSet(s.SetNumber, s.WeightKg, s.Reps, s.Rpe))])
                : null;

        SessionExerciseResponse[] entries =
        [
            .. positions.Select(position =>
            {
                Guid exerciseId = ExerciseAt(position);
                WorkoutExerciseView? line = prescribed.GetValueOrDefault(position);

                return new SessionExerciseResponse(
                    position,
                    exerciseId,
                    line?.Exercise ?? SummaryOf(exerciseId),
                    line is null ? null : PrescriptionResponse.From(line),
                    LastOf(exerciseId),
                    [.. logged[position].OrderBy(s => s.SetNumber).Select(SetResponse.From)]);
            }),
        ];

        var totals = SessionTotals.Of(sets, session.StartedAt, session.CompletedAt);

        return new SessionResponse(
            session.Id,
            session.ClientId,
            session.AssignmentId,
            session.WorkoutName,
            session.Status,
            session.StartedAt,
            session.CompletedAt,
            session.Notes,
            entries,
            new SessionTotalsResponse(
                totals.TotalVolumeKg,
                totals.CompletedSets,
                totals.TotalReps,
                totals.DurationSeconds,
                [.. records.Select(r => PersonalRecordResponse.From(r, SummaryOf(r.ExerciseId)))]));
    }
}

internal sealed record StartSessionCommand(Guid ClientId, Guid? Id, Guid? AssignmentId, string? WorkoutName, DateTime? StartedAt) : ICommand<StartedSession>;

internal sealed class StartSessionValidator : AbstractValidator<StartSessionCommand>
{
    public StartSessionValidator(TimeProvider timeProvider)
    {
        RuleFor(c => c.Id).Must(id => id != Guid.Empty).WithMessage("Must not be empty.");
        RuleFor(c => c.WorkoutName)
            .MaximumLength(Session.NameMaxLength)
            .Must((c, name) => c.AssignmentId is not null || !string.IsNullOrWhiteSpace(name))
            .WithMessage("Either 'assignmentId' or 'workoutName' is required.");
        RuleFor(c => c.StartedAt)
            .Must(at => at is null || at.Value.AsUtc() <= timeProvider.UtcNow() + Session.AllowedClockSkew)
            .WithMessage("Must not be more than 5 minutes in the future.");
    }
}

internal sealed class StartSessionHandler(
    ISessionRepository sessions,
    IAssignmentRepository assignments,
    IClientRepository clients,
    IExerciseRepository exercises,
    TimeProvider timeProvider) : ICommandHandler<StartSessionCommand, StartedSession>
{
    public async Task<Result<StartedSession>> Handle(StartSessionCommand command, CancellationToken cancellationToken)
    {
        if (command.Id is { } requestedId && await sessions.GetAsync(requestedId, forUpdate: false, cancellationToken) is { } existing)
        {
            // Replaying a create (offline clients retry) returns the session instead of failing.
            return existing.ClientId == command.ClientId
                ? new StartedSession(await SessionViews.ToResponseAsync(existing, clients, sessions, exercises, cancellationToken), Created: false)
                : SessionErrors.IdInUse(requestedId);
        }

        return await clients.GetAsync(command.ClientId, cancellationToken)
            .ToResult(ClientErrors.NotFound(command.ClientId))
            .Bind(client => client.EnsureActive())
            .Bind(client => StartAsync(command, client, cancellationToken));
    }

    private async Task<Result<StartedSession>> StartAsync(StartSessionCommand command, ClientProfile client, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();
        Guid sessionId = command.Id ?? Ids.New(timeProvider);

        if (command.AssignmentId is not { } assignmentId)
        {
            return await CreateAsync(command, client, assignment: null, sessionId, now, cancellationToken);
        }

        return await assignments.GetAsync(assignmentId, forUpdate: true, cancellationToken)
            .ToResult(AssignmentErrors.NotFound(assignmentId))
            .Ensure(found => found.ClientId == client.Id, AssignmentErrors.NotFound(assignmentId))
            .Bind(found => found.StartSession(sessionId, now))
            .Bind(started => CreateAsync(command, client, started, sessionId, now, cancellationToken));
    }

    private async Task<Result<StartedSession>> CreateAsync(
        StartSessionCommand command,
        ClientProfile client,
        Assignment? assignment,
        Guid sessionId,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var session = Session.Start(sessionId, client, assignment, command.WorkoutName, command.StartedAt?.AsUtc() ?? now, now);
        await sessions.AddAsync(session, cancellationToken);

        if (assignment is not null)
        {
            await assignments.UpdateAsync(assignment, cancellationToken);
        }

        return new StartedSession(await SessionViews.ToResponseAsync(session, clients, sessions, exercises, cancellationToken), Created: true);
    }
}

internal sealed record GetSessionQuery(Viewer Viewer, Guid SessionId) : IQuery<SessionResponse>;

internal sealed class GetSessionHandler(ISessionRepository sessions, IClientRepository clients, IExerciseRepository exercises)
    : IQueryHandler<GetSessionQuery, SessionResponse>
{
    public Task<Result<SessionResponse>> Handle(GetSessionQuery query, CancellationToken cancellationToken) =>
        sessions.GetAccessibleAsync(query.Viewer, query.SessionId, forUpdate: false, cancellationToken)
            .Map(session => SessionViews.ToResponseAsync(session, clients, sessions, exercises, cancellationToken));
}

internal sealed record ListSessionsQuery(Viewer Viewer, Guid? ClientId, string? Status, DateOnly? From, DateOnly? To, int Page, int PageSize)
    : IQuery<Paged<SessionSummaryResponse>>, IPagedQuery;

internal sealed class ListSessionsValidator : PagedQueryValidator<ListSessionsQuery>
{
    public ListSessionsValidator()
    {
        RuleFor(q => q.Status).KebabEnum<ListSessionsQuery, SessionStatus>();
        RuleFor(q => q.To).Must((q, to) => q.From is null || to is null || to >= q.From).WithMessage("Must not be before 'from'.");
    }
}

internal sealed class ListSessionsHandler(IClientRepository clients, IDbSession db) : IQueryHandler<ListSessionsQuery, Paged<SessionSummaryResponse>>
{
    private const string Filter =
        """
        (@TrainerId::uuid IS NULL OR s.trainer_id = @TrainerId::uuid) AND (@ClientId::uuid IS NULL OR s.client_id = @ClientId::uuid)
        AND (@Status::text IS NULL OR s.status = @Status::text)
        AND (@From::date IS NULL OR (s.started_at AT TIME ZONE c.time_zone)::date >= @From::date)
        AND (@To::date IS NULL OR (s.started_at AT TIME ZONE c.time_zone)::date <= @To::date)
        """;

    public Task<Result<Paged<SessionSummaryResponse>>> Handle(ListSessionsQuery query, CancellationToken cancellationToken) =>
        clients.ScopeAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(scope => ListAsync(query, scope, cancellationToken));

    private async Task<Paged<SessionSummaryResponse>> ListAsync(ListSessionsQuery query, ListScope scope, CancellationToken cancellationToken)
    {
        var parameters = new
        {
            scope.TrainerId,
            scope.ClientId,
            Status = KebabCase.TryParse(query.Status, out SessionStatus status) ? status.ToString() : null,
            query.From,
            query.To,
            query.PageSize,
            Offset = (query.Page - 1) * query.PageSize,
        };

        int total = await db.QuerySingleAsync<int>(
            $"SELECT count(*)::int FROM training.sessions s JOIN training.clients c ON c.id = s.client_id WHERE {Filter}",
            parameters,
            cancellationToken);
        IReadOnlyList<SessionSummaryRow> rows = await db.QueryAsync<SessionSummaryRow>(
            $"{SessionSummarySql.Select} WHERE {Filter} ORDER BY s.started_at DESC, s.id DESC LIMIT @PageSize OFFSET @Offset",
            parameters,
            cancellationToken);

        return new Paged<SessionSummaryResponse>([.. rows.Select(r => r.ToSummary())], query.Page, query.PageSize, total);
    }
}

internal sealed record UpdateSessionCommand(Guid ClientId, Guid SessionId, string WorkoutName, string? Notes) : ICommand<SessionResponse>;

internal sealed class UpdateSessionValidator : AbstractValidator<UpdateSessionCommand>
{
    public UpdateSessionValidator()
    {
        RuleFor(c => c.WorkoutName).ValidName(Session.NameMaxLength);
        RuleFor(c => c.Notes).MaximumLength(Session.NotesMaxLength);
    }
}

internal sealed class UpdateSessionHandler(ISessionRepository sessions, IClientRepository clients, IExerciseRepository exercises, TimeProvider timeProvider)
    : ICommandHandler<UpdateSessionCommand, SessionResponse>
{
    public Task<Result<SessionResponse>> Handle(UpdateSessionCommand command, CancellationToken cancellationToken) =>
        sessions.GetAccessibleAsync(Viewer.Client(command.ClientId, null), command.SessionId, forUpdate: true, cancellationToken)
            .Tap(session => session.Rename(command.WorkoutName, command.Notes, timeProvider.UtcNow()))
            .Tap(session => sessions.UpdateAsync(session, cancellationToken))
            .Map(session => SessionViews.ToResponseAsync(session, clients, sessions, exercises, cancellationToken));
}

internal sealed record UpsertSetCommand(
    Guid ClientId,
    Guid SessionId,
    Guid SetId,
    Guid ExerciseId,
    int Position,
    int SetNumber,
    decimal? WeightKg,
    int? Reps,
    int? DurationSeconds,
    decimal? DistanceMeters,
    decimal? Rpe,
    bool Completed,
    string? Notes) : ICommand<SetResponse>
{
    public static UpsertSetCommand From(Guid clientId, Guid sessionId, Guid setId, SetRequest request) =>
        new(
            clientId,
            sessionId,
            setId,
            request.ExerciseId,
            request.Position,
            request.SetNumber,
            request.WeightKg,
            request.Reps,
            request.DurationSeconds,
            request.DistanceMeters,
            request.Rpe,
            request.Completed,
            request.Notes);
}

internal sealed class UpsertSetValidator : AbstractValidator<UpsertSetCommand>
{
    public UpsertSetValidator()
    {
        RuleFor(c => c.ExerciseId).NotEmpty();
        RuleFor(c => c.Position).InclusiveBetween(1, SessionSet.MaxPosition);
        RuleFor(c => c.SetNumber).InclusiveBetween(1, SessionSet.MaxSetNumber);
        RuleFor(c => c.WeightKg).InRange(0, SessionSet.MaxWeightKg);
        RuleFor(c => c.Reps).InclusiveBetween(0, SessionSet.MaxReps);
        RuleFor(c => c.DurationSeconds).InclusiveBetween(0, SessionSet.MaxDurationSeconds);
        RuleFor(c => c.DistanceMeters).InRange(0, SessionSet.MaxDistanceMeters);
        RuleFor(c => c.Rpe).Must(rpe => rpe is null || Decimals.IsRpe(rpe.Value)).WithMessage("Must be between 1 and 10 in steps of 0.5.");
        RuleFor(c => c.Notes).MaximumLength(SessionSet.NotesMaxLength);
    }
}

internal sealed class UpsertSetHandler(ISessionRepository sessions, IExerciseRepository exercises, TimeProvider timeProvider)
    : ICommandHandler<UpsertSetCommand, SetResponse>
{
    public Task<Result<SetResponse>> Handle(UpsertSetCommand command, CancellationToken cancellationToken) =>
        sessions.GetAccessibleAsync(Viewer.Client(command.ClientId, null), command.SessionId, forUpdate: true, cancellationToken)
            .Bind(session => session.EnsureInProgress())
            .Bind(session => SaveAsync(command, session, cancellationToken));

    private async Task<Result<SetResponse>> SaveAsync(UpsertSetCommand command, Session session, CancellationToken cancellationToken)
    {
        if (await sessions.GetSessionIdOfSetAsync(command.SetId, cancellationToken) is { } owner && owner != session.Id)
        {
            return SessionErrors.SetIdInUse(command.SetId);
        }

        Exercise? exercise = await exercises.GetAsync(command.ExerciseId, cancellationToken);

        if (exercise is null || !exercise.IsVisibleTo(Viewer.Client(session.ClientId, session.TrainerId)))
        {
            NotFoundError error = ExerciseErrors.NotFound(command.ExerciseId);
            return ValidationError.For("exerciseId", error.Message, error.Code);
        }

        SessionSet set = new(
            command.SetId,
            session.Id,
            command.ExerciseId,
            command.Position,
            command.SetNumber,
            command.WeightKg,
            command.Reps,
            command.DurationSeconds,
            command.DistanceMeters,
            command.Rpe,
            command.Completed,
            Text.NullIfBlank(command.Notes),
            timeProvider.UtcNow());

        await sessions.UpsertSetAsync(set, cancellationToken);
        return SetResponse.From(set);
    }
}

internal sealed record DeleteSetCommand(Guid ClientId, Guid SessionId, Guid SetId) : ICommand<Unit>;

internal sealed class DeleteSetHandler(ISessionRepository sessions) : ICommandHandler<DeleteSetCommand, Unit>
{
    public Task<Result<Unit>> Handle(DeleteSetCommand command, CancellationToken cancellationToken) =>
        sessions.GetAccessibleAsync(Viewer.Client(command.ClientId, null), command.SessionId, forUpdate: true, cancellationToken)
            .Bind(session => session.EnsureInProgress())
            .Tap(session => sessions.DeleteSetAsync(session.Id, command.SetId, cancellationToken))
            .Map(_ => default(Unit));
}

internal sealed record CompleteSessionCommand(Guid ClientId, Guid SessionId, DateTime? CompletedAt, string? Notes) : ICommand<SessionResponse>;

internal sealed class CompleteSessionValidator : AbstractValidator<CompleteSessionCommand>
{
    public CompleteSessionValidator(TimeProvider timeProvider)
    {
        RuleFor(c => c.CompletedAt)
            .Must(at => at is null || at.Value.AsUtc() <= timeProvider.UtcNow() + Session.AllowedClockSkew)
            .WithMessage("Must not be more than 5 minutes in the future.");
        RuleFor(c => c.Notes).MaximumLength(Session.NotesMaxLength);
    }
}

internal sealed class CompleteSessionHandler(
    ISessionRepository sessions,
    IAssignmentRepository assignments,
    IClientRepository clients,
    IExerciseRepository exercises,
    TimeProvider timeProvider) : ICommandHandler<CompleteSessionCommand, SessionResponse>
{
    public Task<Result<SessionResponse>> Handle(CompleteSessionCommand command, CancellationToken cancellationToken)
    {
        DateTime now = timeProvider.UtcNow();

        return sessions.GetAccessibleAsync(Viewer.Client(command.ClientId, null), command.SessionId, forUpdate: true, cancellationToken)
            .Bind(session => session
                .Complete(command.CompletedAt?.AsUtc() ?? (now > session.StartedAt ? now : session.StartedAt), command.Notes, now)
                .Map(completedNow => (Session: session, CompletedNow: completedNow)))
            .Tap(result => result.CompletedNow ? FinishAsync(result.Session, now, cancellationToken) : Task.CompletedTask)
            .Map(result => SessionViews.ToResponseAsync(result.Session, clients, sessions, exercises, cancellationToken));
    }

    /// <summary>
    /// Records the personal records the session set and completes its assignment.
    /// </summary>
    private async Task FinishAsync(Session session, DateTime now, CancellationToken cancellationToken)
    {
        DateTime completedAt = session.CompletedAt ?? now;
        IReadOnlyList<SessionSet> sets = await sessions.GetSetsAsync(session.Id, cancellationToken);
        LoggedSet[] logged = [.. sets.Where(s => s.Completed).Select(s => new LoggedSet(s.SessionId, s.ExerciseId, s.WeightKg, s.Reps))];
        IReadOnlyList<LoggedSet> history = logged.Length == 0
            ? []
            : await sessions.GetHistoryAsync(session.ClientId, [.. logged.Select(s => s.ExerciseId).Distinct()], session.Id, cancellationToken);

        PersonalRecord[] records =
        [
            .. PersonalRecords.Detect(logged, history).Select(c => new PersonalRecord(
                Ids.New(timeProvider),
                session.ClientId,
                session.TrainerId,
                c.ExerciseId,
                c.Type,
                c.Value,
                c.PreviousValue,
                c.WeightKg,
                c.Reps,
                session.Id,
                completedAt)),
        ];

        await sessions.UpdateAsync(session, cancellationToken);
        await sessions.AddPersonalRecordsAsync(records, cancellationToken);

        if (session.AssignmentId is { } assignmentId && await assignments.GetAsync(assignmentId, forUpdate: true, cancellationToken) is { } assignment)
        {
            assignment.Complete(completedAt, now);
            await assignments.UpdateAsync(assignment, cancellationToken);
        }
    }
}

internal sealed record DiscardSessionCommand(Guid ClientId, Guid SessionId) : ICommand<Unit>;

internal sealed class DiscardSessionHandler(ISessionRepository sessions, IAssignmentRepository assignments, TimeProvider timeProvider)
    : ICommandHandler<DiscardSessionCommand, Unit>
{
    public Task<Result<Unit>> Handle(DiscardSessionCommand command, CancellationToken cancellationToken) =>
        sessions.GetAccessibleAsync(Viewer.Client(command.ClientId, null), command.SessionId, forUpdate: true, cancellationToken)
            .Bind(session => session.EnsureInProgress())
            .Tap(session => DiscardAsync(session, cancellationToken))
            .Map(_ => default(Unit));

    private async Task DiscardAsync(Session session, CancellationToken cancellationToken)
    {
        await sessions.DeleteAsync(session.Id, cancellationToken);

        if (session.AssignmentId is { } assignmentId && await assignments.GetAsync(assignmentId, forUpdate: true, cancellationToken) is { } assignment)
        {
            assignment.DetachSession(timeProvider.UtcNow());
            await assignments.UpdateAsync(assignment, cancellationToken);
        }
    }
}

internal sealed class SessionEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/sessions").WithTags("Sessions");

        group.MapPost(
                string.Empty,
                async (StartSessionRequest request, ICommandHandler<StartSessionCommand, StartedSession> handler, HttpContext http, CancellationToken cancellationToken) =>
                {
                    Result<StartedSession> result = await handler.Handle(
                        new StartSessionCommand(http.User.GetUserId(), request.Id, request.AssignmentId, request.WorkoutName, request.StartedAt),
                        cancellationToken);

                    return result.Match<StartedSession, Results<Created<SessionResponse>, Ok<SessionResponse>, ProblemHttpResult>>(
                        started => started.Created
                            ? TypedResults.Created($"/api/sessions/{started.Session.Id}", started.Session)
                            : TypedResults.Ok(started.Session),
                        error => error.ToProblem());
                })
            .WithName("StartSession")
            .RequirePermission(Permissions.SessionsLog);

        group.MapGet(
                string.Empty,
                (Guid? clientId, string? status, DateOnly? from, DateOnly? to, int? page, int? pageSize, IQueryHandler<ListSessionsQuery, Paged<SessionSummaryResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListSessionsQuery(http.User.ToViewer(), clientId, status, from, to, page ?? 1, pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ToOk())
            .WithName("ListSessions")
            .RequirePermission(Permissions.SessionsRead);

        group.MapGet(
                "/{id:guid}",
                (Guid id, IQueryHandler<GetSessionQuery, SessionResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new GetSessionQuery(http.User.ToViewer(), id), cancellationToken).ToOk())
            .WithName("GetSession")
            .RequirePermission(Permissions.SessionsRead);

        group.MapPut(
                "/{id:guid}",
                (Guid id, UpdateSessionRequest request, ICommandHandler<UpdateSessionCommand, SessionResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new UpdateSessionCommand(http.User.GetUserId(), id, request.WorkoutName, request.Notes), cancellationToken).ToOk())
            .WithName("UpdateSession")
            .RequirePermission(Permissions.SessionsLog);

        group.MapPut(
                "/{id:guid}/sets/{setId:guid}",
                (Guid id, Guid setId, SetRequest request, ICommandHandler<UpsertSetCommand, SetResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(UpsertSetCommand.From(http.User.GetUserId(), id, setId, request), cancellationToken).ToOk())
            .WithName("UpsertSessionSet")
            .RequirePermission(Permissions.SessionsLog);

        group.MapDelete(
                "/{id:guid}/sets/{setId:guid}",
                (Guid id, Guid setId, ICommandHandler<DeleteSetCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteSetCommand(http.User.GetUserId(), id, setId), cancellationToken).ToNoContent())
            .WithName("DeleteSessionSet")
            .RequirePermission(Permissions.SessionsLog);

        group.MapPost(
                "/{id:guid}/complete",
                (Guid id, CompleteSessionRequest? request, ICommandHandler<CompleteSessionCommand, SessionResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new CompleteSessionCommand(http.User.GetUserId(), id, request?.CompletedAt, request?.Notes), cancellationToken).ToOk())
            .WithName("CompleteSession")
            .RequirePermission(Permissions.SessionsLog);

        group.MapDelete(
                "/{id:guid}",
                (Guid id, ICommandHandler<DiscardSessionCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DiscardSessionCommand(http.User.GetUserId(), id), cancellationToken).ToNoContent())
            .WithName("DiscardSession")
            .RequirePermission(Permissions.SessionsLog);
    }
}
