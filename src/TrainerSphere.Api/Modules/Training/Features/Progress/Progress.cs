using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.Progress;

internal sealed record ProgressExerciseResponse(Guid ExerciseId, ExerciseSummary Exercise, int SessionCount, DateTime LastPerformedAt);

internal sealed record ExerciseProgressResponse(ExerciseSummary Exercise, IReadOnlyList<ProgressPoint> Points);

internal sealed record ProgressSummaryResponse(
    IReadOnlyList<WeekSummary> Weeks,
    int TotalSessions,
    decimal? Adherence7d,
    decimal? Adherence28d,
    int StreakWeeks);

internal static class ProgressViews
{
    public static async Task<IReadOnlyList<PersonalRecordResponse>> ToResponsesAsync(IReadOnlyList<PersonalRecord> records, IExerciseRepository exercises, CancellationToken cancellationToken)
    {
        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(records.Select(r => r.ExerciseId), cancellationToken);

        return [.. records.Select(r => PersonalRecordResponse.From(r, summaries.GetValueOrDefault(r.ExerciseId) ?? new ExerciseSummary(r.ExerciseId, null, string.Empty, null)))];
    }
}

internal sealed record ListPersonalRecordsQuery(Viewer Viewer, Guid ClientId) : IQuery<IReadOnlyList<PersonalRecordResponse>>;

internal sealed class ListPersonalRecordsHandler(IClientRepository clients, IExerciseRepository exercises, IDbSession db)
    : IQueryHandler<ListPersonalRecordsQuery, IReadOnlyList<PersonalRecordResponse>>
{
    public Task<Result<IReadOnlyList<PersonalRecordResponse>>> Handle(ListPersonalRecordsQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(client => ListAsync(client, cancellationToken));

    private async Task<IReadOnlyList<PersonalRecordResponse>> ListAsync(ClientProfile client, CancellationToken cancellationToken)
    {
        // Records only ever improve, so the latest record per exercise and type is the current best.
        IReadOnlyList<PersonalRecordRow> rows = await db.QueryAsync<PersonalRecordRow>(
            $"""
            SELECT DISTINCT ON (exercise_id, type) {SessionRepository.PersonalRecordColumns}
            FROM training.personal_records
            WHERE client_id = @ClientId
            ORDER BY exercise_id, type, value DESC, achieved_at DESC
            """,
            new { ClientId = client.Id },
            cancellationToken);

        IReadOnlyList<PersonalRecordResponse> responses = await ProgressViews.ToResponsesAsync([.. rows.Select(r => r.ToDomain())], exercises, cancellationToken);

        return [.. responses.OrderBy(r => r.Exercise.Name, StringComparer.OrdinalIgnoreCase).ThenBy(r => r.Type)];
    }
}

internal sealed record PersonalRecordHistoryQuery(Viewer Viewer, Guid ClientId, Guid? ExerciseId, int Page, int PageSize)
    : IQuery<Paged<PersonalRecordResponse>>, IPagedQuery;

internal sealed class PersonalRecordHistoryValidator : PagedQueryValidator<PersonalRecordHistoryQuery>;

internal sealed class PersonalRecordHistoryHandler(IClientRepository clients, IExerciseRepository exercises, IDbSession db)
    : IQueryHandler<PersonalRecordHistoryQuery, Paged<PersonalRecordResponse>>
{
    private const string Filter = "client_id = @ClientId AND (@ExerciseId::uuid IS NULL OR exercise_id = @ExerciseId::uuid)";

    public Task<Result<Paged<PersonalRecordResponse>>> Handle(PersonalRecordHistoryQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(client => ListAsync(query, client, cancellationToken));

    private async Task<Paged<PersonalRecordResponse>> ListAsync(PersonalRecordHistoryQuery query, ClientProfile client, CancellationToken cancellationToken)
    {
        var parameters = new { ClientId = client.Id, query.ExerciseId, query.PageSize, Offset = (query.Page - 1) * query.PageSize };

        int total = await db.QuerySingleAsync<int>($"SELECT count(*)::int FROM training.personal_records WHERE {Filter}", parameters, cancellationToken);
        IReadOnlyList<PersonalRecordRow> rows = await db.QueryAsync<PersonalRecordRow>(
            $"""
            SELECT {SessionRepository.PersonalRecordColumns} FROM training.personal_records
            WHERE {Filter}
            ORDER BY achieved_at DESC, id DESC
            LIMIT @PageSize OFFSET @Offset
            """,
            parameters,
            cancellationToken);

        return new Paged<PersonalRecordResponse>(
            await ProgressViews.ToResponsesAsync([.. rows.Select(r => r.ToDomain())], exercises, cancellationToken),
            query.Page,
            query.PageSize,
            total);
    }
}

internal sealed record ListProgressExercisesQuery(Viewer Viewer, Guid ClientId) : IQuery<IReadOnlyList<ProgressExerciseResponse>>;

internal sealed class ListProgressExercisesHandler(IClientRepository clients, IExerciseRepository exercises, IDbSession db)
    : IQueryHandler<ListProgressExercisesQuery, IReadOnlyList<ProgressExerciseResponse>>
{
    public Task<Result<IReadOnlyList<ProgressExerciseResponse>>> Handle(ListProgressExercisesQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(client => ListAsync(client, cancellationToken));

    private async Task<IReadOnlyList<ProgressExerciseResponse>> ListAsync(ClientProfile client, CancellationToken cancellationToken)
    {
        IReadOnlyList<ProgressExerciseRow> rows = await db.QueryAsync<ProgressExerciseRow>(
            """
            SELECT ss.exercise_id, count(DISTINCT s.id)::int AS session_count, max(s.completed_at) AS last_performed_at
            FROM training.session_sets ss
            JOIN training.sessions s ON s.id = ss.session_id
            WHERE s.client_id = @ClientId AND s.status = 'Completed' AND ss.completed
            GROUP BY ss.exercise_id
            ORDER BY max(s.completed_at) DESC, ss.exercise_id
            """,
            new { ClientId = client.Id },
            cancellationToken);

        IReadOnlyDictionary<Guid, ExerciseSummary> summaries = await exercises.GetSummariesAsync(rows.Select(r => r.ExerciseId), cancellationToken);

        return
        [
            .. rows.Select(r => new ProgressExerciseResponse(
                r.ExerciseId,
                summaries.GetValueOrDefault(r.ExerciseId) ?? new ExerciseSummary(r.ExerciseId, null, string.Empty, null),
                r.SessionCount,
                r.LastPerformedAt)),
        ];
    }

    internal sealed record ProgressExerciseRow
    {
        public Guid ExerciseId { get; init; }

        public int SessionCount { get; init; }

        public DateTime LastPerformedAt { get; init; }
    }
}

internal sealed record ExerciseProgressQuery(Viewer Viewer, Guid ClientId, Guid ExerciseId, DateOnly? From, DateOnly? To) : IQuery<ExerciseProgressResponse>;

internal sealed class ExerciseProgressValidator : AbstractValidator<ExerciseProgressQuery>
{
    public ExerciseProgressValidator() =>
        RuleFor(q => q.To).Must((q, to) => q.From is null || to is null || to >= q.From).WithMessage("'to' must not be before 'from'.");
}

internal sealed class ExerciseProgressHandler(IClientRepository clients, IExerciseRepository exercises, IDbSession db)
    : IQueryHandler<ExerciseProgressQuery, ExerciseProgressResponse>
{
    public Task<Result<ExerciseProgressResponse>> Handle(ExerciseProgressQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Bind(client => LoadAsync(query, client, cancellationToken));

    private async Task<Result<ExerciseProgressResponse>> LoadAsync(ExerciseProgressQuery query, ClientProfile client, CancellationToken cancellationToken)
    {
        Exercise? exercise = await exercises.GetAsync(query.ExerciseId, cancellationToken);

        if (exercise is null || !exercise.IsVisibleTo(Viewer.Client(client.Id, client.TrainerId)))
        {
            return ExerciseErrors.NotFound(query.ExerciseId);
        }

        IReadOnlyList<ProgressSetRow> rows = await db.QueryAsync<ProgressSetRow>(
            """
            SELECT s.id AS session_id, s.started_at, ss.weight_kg, ss.reps
            FROM training.session_sets ss
            JOIN training.sessions s ON s.id = ss.session_id
            WHERE s.client_id = @ClientId AND s.status = 'Completed' AND ss.completed AND ss.exercise_id = @ExerciseId
            """,
            new { ClientId = client.Id, query.ExerciseId },
            cancellationToken);

        IReadOnlyList<ProgressPoint> points = ExerciseProgress.Points(rows.Select(r => new ProgressSet(r.SessionId, r.StartedAt, r.WeightKg, r.Reps)), TimeZones.FindOrUtc(client.TimeZone));

        return new ExerciseProgressResponse(
            exercise.Summary,
            [.. points.Where(p => (query.From is null || p.Date >= query.From) && (query.To is null || p.Date <= query.To))]);
    }

    internal sealed record ProgressSetRow
    {
        public Guid SessionId { get; init; }

        public DateTime StartedAt { get; init; }

        public decimal? WeightKg { get; init; }

        public int? Reps { get; init; }
    }
}

internal sealed record ProgressSummaryQuery(Viewer Viewer, Guid ClientId, int Weeks) : IQuery<ProgressSummaryResponse>;

internal sealed class ProgressSummaryValidator : AbstractValidator<ProgressSummaryQuery>
{
    public const int DefaultWeeks = 12;
    public const int MaxWeeks = 52;

    public ProgressSummaryValidator() => RuleFor(q => q.Weeks).InclusiveBetween(1, MaxWeeks);
}

internal sealed class ProgressSummaryHandler(
    IClientRepository clients,
    ISessionRepository sessions,
    IAssignmentRepository assignments,
    TimeProvider timeProvider) : IQueryHandler<ProgressSummaryQuery, ProgressSummaryResponse>
{
    public Task<Result<ProgressSummaryResponse>> Handle(ProgressSummaryQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(client => SummarizeAsync(query, client, cancellationToken));

    private async Task<ProgressSummaryResponse> SummarizeAsync(ProgressSummaryQuery query, ClientProfile client, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone = TimeZones.FindOrUtc(client.TimeZone);
        DateOnly today = timeProvider.Today(client.TimeZone);
        IReadOnlyList<SessionVolume> completed = await sessions.GetCompletedAsync(client.Id, cancellationToken);
        IReadOnlyList<Assignment> recent = await assignments.ListAsync(trainerId: null, client.Id, today.AddDays(-27), today, cancellationToken);
        ScheduledItem[] items = [.. recent.Select(a => new ScheduledItem(a.Date, a.StoredStatus))];

        return new ProgressSummaryResponse(
            ExerciseProgress.Weeks(completed, zone, today, query.Weeks),
            completed.Count,
            Adherence.LastDays(items, today, 7).Rate,
            Adherence.LastDays(items, today, 28).Rate,
            Streaks.Weeks(completed.Select(s => LocalDates.Of(s.StartedAt, zone)), today));
    }
}

internal sealed class ProgressEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/clients/{clientId:guid}").WithTags("Progress").RequirePermission(Permissions.ProgressRead);

        group.MapGet(
                "/personal-records",
                (Guid clientId, IQueryHandler<ListPersonalRecordsQuery, IReadOnlyList<PersonalRecordResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListPersonalRecordsQuery(http.User.ToViewer(), clientId), cancellationToken).ToOk())
            .WithName("ListPersonalRecords");

        group.MapGet(
                "/personal-records/history",
                (Guid clientId, Guid? exerciseId, int? page, int? pageSize, IQueryHandler<PersonalRecordHistoryQuery, Paged<PersonalRecordResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new PersonalRecordHistoryQuery(http.User.ToViewer(), clientId, exerciseId, page ?? 1, pageSize ?? PageRequest.DefaultPageSize), cancellationToken).ToOk())
            .WithName("ListPersonalRecordHistory");

        group.MapGet(
                "/progress/exercises",
                (Guid clientId, IQueryHandler<ListProgressExercisesQuery, IReadOnlyList<ProgressExerciseResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListProgressExercisesQuery(http.User.ToViewer(), clientId), cancellationToken).ToOk())
            .WithName("ListProgressExercises");

        group.MapGet(
                "/progress/exercises/{exerciseId:guid}",
                (Guid clientId, Guid exerciseId, DateOnly? from, DateOnly? to, IQueryHandler<ExerciseProgressQuery, ExerciseProgressResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ExerciseProgressQuery(http.User.ToViewer(), clientId, exerciseId, from, to), cancellationToken).ToOk())
            .WithName("GetExerciseProgress");

        group.MapGet(
                "/progress/summary",
                (Guid clientId, int? weeks, IQueryHandler<ProgressSummaryQuery, ProgressSummaryResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ProgressSummaryQuery(http.User.ToViewer(), clientId, weeks ?? ProgressSummaryValidator.DefaultWeeks), cancellationToken).ToOk())
            .WithName("GetProgressSummary");
    }
}
