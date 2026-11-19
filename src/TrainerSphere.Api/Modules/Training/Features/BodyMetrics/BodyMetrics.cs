using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features.BodyMetrics;

internal sealed record BodyMetricRequest(
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes);

internal sealed record ListBodyMetricsQuery(Viewer Viewer, Guid ClientId, DateOnly? From, DateOnly? To) : IQuery<IReadOnlyList<BodyMetricResponse>>;

internal sealed class ListBodyMetricsValidator : AbstractValidator<ListBodyMetricsQuery>
{
    public ListBodyMetricsValidator() =>
        RuleFor(q => q.To).Must((q, to) => q.From is null || to is null || to >= q.From).WithMessage("Must not be before 'from'.");
}

internal sealed class ListBodyMetricsHandler(IClientRepository clients, IBodyMetricRepository metrics)
    : IQueryHandler<ListBodyMetricsQuery, IReadOnlyList<BodyMetricResponse>>
{
    public Task<Result<IReadOnlyList<BodyMetricResponse>>> Handle(ListBodyMetricsQuery query, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(query.Viewer, query.ClientId, cancellationToken)
            .Map(async client => (IReadOnlyList<BodyMetricResponse>)[.. (await metrics.ListAsync(client.Id, query.From, query.To, cancellationToken)).Select(BodyMetricResponse.From)]);
}

internal sealed record PutBodyMetricCommand(
    Viewer Viewer,
    Guid ClientId,
    DateOnly Date,
    decimal? WeightKg,
    decimal? BodyFatPercent,
    decimal? WaistCm,
    decimal? ChestCm,
    decimal? HipsCm,
    decimal? ArmCm,
    decimal? ThighCm,
    int? RestingHeartRate,
    string? Notes) : ICommand<BodyMetricResponse>
{
    public static PutBodyMetricCommand From(Viewer viewer, Guid clientId, DateOnly date, BodyMetricRequest request) =>
        new(
            viewer,
            clientId,
            date,
            request.WeightKg,
            request.BodyFatPercent,
            request.WaistCm,
            request.ChestCm,
            request.HipsCm,
            request.ArmCm,
            request.ThighCm,
            request.RestingHeartRate,
            request.Notes);

    public BodyMetric ToMetric(DateTime now) =>
        new(ClientId, Date, WeightKg, BodyFatPercent, WaistCm, ChestCm, HipsCm, ArmCm, ThighCm, RestingHeartRate, Text.NullIfBlank(Notes), Viewer.Role, now);
}

internal sealed class PutBodyMetricValidator : AbstractValidator<PutBodyMetricCommand>
{
    public const string MeasurementsField = "Measurements";

    public PutBodyMetricValidator()
    {
        RuleFor(c => c.WeightKg).InRange(BodyMetric.MinWeightKg, BodyMetric.MaxWeightKg);
        RuleFor(c => c.BodyFatPercent).InRange(BodyMetric.MinBodyFat, BodyMetric.MaxBodyFat);
        RuleFor(c => c.WaistCm).InRange(BodyMetric.MinCircumference, BodyMetric.MaxCircumference);
        RuleFor(c => c.ChestCm).InRange(BodyMetric.MinCircumference, BodyMetric.MaxCircumference);
        RuleFor(c => c.HipsCm).InRange(BodyMetric.MinCircumference, BodyMetric.MaxCircumference);
        RuleFor(c => c.ArmCm).InRange(BodyMetric.MinCircumference, BodyMetric.MaxCircumference);
        RuleFor(c => c.ThighCm).InRange(BodyMetric.MinCircumference, BodyMetric.MaxCircumference);
        RuleFor(c => c.RestingHeartRate).InclusiveBetween(BodyMetric.MinHeartRate, BodyMetric.MaxHeartRate);
        RuleFor(c => c.Notes).MaximumLength(BodyMetric.NotesMaxLength);
        RuleFor(c => c)
            .Must(c => c.ToMetric(DateTime.UnixEpoch).HasAnyMeasurement)
            .OverridePropertyName(MeasurementsField)
            .WithMessage("Enter at least one measurement.");
    }
}

internal sealed class PutBodyMetricHandler(IClientRepository clients, IBodyMetricRepository metrics, TimeProvider timeProvider)
    : ICommandHandler<PutBodyMetricCommand, BodyMetricResponse>
{
    public Task<Result<BodyMetricResponse>> Handle(PutBodyMetricCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(command.Viewer, command.ClientId, cancellationToken)
            .Bind(client => SaveAsync(command, client, cancellationToken));

    private async Task<Result<BodyMetricResponse>> SaveAsync(PutBodyMetricCommand command, ClientProfile client, CancellationToken cancellationToken)
    {
        if (!BodyMetric.IsDateAllowed(command.Date, timeProvider.Today(client.TimeZone)))
        {
            return ValidationError.For("date", "Measurements cannot be recorded for a future date.");
        }

        BodyMetric metric = command.ToMetric(timeProvider.UtcNow());
        await metrics.UpsertAsync(metric, cancellationToken);
        return BodyMetricResponse.From(metric);
    }
}

internal sealed record DeleteBodyMetricCommand(Viewer Viewer, Guid ClientId, DateOnly Date) : ICommand<Unit>;

internal sealed class DeleteBodyMetricHandler(IClientRepository clients, IBodyMetricRepository metrics) : ICommandHandler<DeleteBodyMetricCommand, Unit>
{
    public Task<Result<Unit>> Handle(DeleteBodyMetricCommand command, CancellationToken cancellationToken) =>
        clients.GetAccessibleAsync(command.Viewer, command.ClientId, cancellationToken)
            .Tap(client => metrics.DeleteAsync(client.Id, command.Date, cancellationToken))
            .Map(_ => default(Unit));
}

internal sealed class BodyMetricEndpoints : IEndpoint
{
    public void MapEndpoint(IEndpointRouteBuilder app)
    {
        RouteGroupBuilder group = app.MapGroup("/api/clients/{clientId:guid}/body-metrics").WithTags("Body metrics");

        group.MapGet(
                string.Empty,
                (Guid clientId, DateOnly? from, DateOnly? to, IQueryHandler<ListBodyMetricsQuery, IReadOnlyList<BodyMetricResponse>> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new ListBodyMetricsQuery(http.User.ToViewer(), clientId, from, to), cancellationToken).ToOk())
            .WithName("ListBodyMetrics")
            .RequirePermission(Permissions.ProgressRead);

        group.MapPut(
                "/{date}",
                (Guid clientId, DateOnly date, BodyMetricRequest request, ICommandHandler<PutBodyMetricCommand, BodyMetricResponse> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(PutBodyMetricCommand.From(http.User.ToViewer(), clientId, date, request), cancellationToken).ToOk())
            .WithName("PutBodyMetric")
            .RequirePermission(Permissions.BodyMetricsWrite);

        group.MapDelete(
                "/{date}",
                (Guid clientId, DateOnly date, ICommandHandler<DeleteBodyMetricCommand, Unit> handler, HttpContext http, CancellationToken cancellationToken) =>
                    handler.Handle(new DeleteBodyMetricCommand(http.User.ToViewer(), clientId, date), cancellationToken).ToNoContent())
            .WithName("DeleteBodyMetric")
            .RequirePermission(Permissions.BodyMetricsWrite);
    }
}
