using System.Security.Claims;
using FluentValidation;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Domain;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Modules.Training.Features;

internal interface IPagedQuery
{
    int Page { get; }

    int PageSize { get; }
}

internal static class TrainingRules
{
    public const int MaxPageSize = 100;
    public const int SearchMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule, int maxLength) =>
        rule.Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("'{PropertyName}' must not be empty.")
            .MaximumLength(maxLength);

    public static IRuleBuilderOptions<T, decimal?> InRange<T>(this IRuleBuilder<T, decimal?> rule, decimal min, decimal max, int decimals = 2) =>
        rule.Must(value => value is null || (value >= min && value <= max && Decimals.HasAtMost(value.Value, decimals)))
            .WithMessage($"'{{PropertyName}}' must be between {min} and {max} with at most {decimals} decimals.");

    public static IRuleBuilderOptions<T, string?> KebabEnum<T, TEnum>(this IRuleBuilder<T, string?> rule)
        where TEnum : struct, Enum =>
        rule.Must(value => value is null || KebabCase.TryParse<TEnum>(value, out _))
            .WithMessage($"'{{PropertyName}}' must be one of: {KebabCase.Allowed<TEnum>()}.");

    public static Viewer ToViewer(this ClaimsPrincipal user) =>
        user.IsTrainer() ? Viewer.Trainer(user.GetUserId()) : Viewer.Client(user.GetUserId(), user.GetTrainerId());

    /// <summary>
    /// Escapes LIKE wildcards so a search is a plain substring match.
    /// </summary>
    public static string? LikePattern(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? null
            : "%" + search.Trim().Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";

    /// <summary>
    /// Npgsql only writes UTC timestamps; JSON times with an offset arrive as local and without one as unspecified.
    /// </summary>
    public static DateTime AsUtc(this DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc),
        };

    /// <summary>
    /// Resolves the client filter of a list: a client always sees only themself; a trainer sees all their clients
    /// or narrows to one of them. Any other client is not found.
    /// </summary>
    public static async Task<Result<ListScope>> ScopeAsync(this IClientRepository clients, Viewer viewer, Guid? clientId, CancellationToken cancellationToken)
    {
        if (viewer.IsClient)
        {
            return clientId is null || clientId == viewer.UserId ? new ListScope(null, viewer.UserId) : ClientErrors.NotFound(clientId.Value);
        }

        if (clientId is not { } id)
        {
            return new ListScope(viewer.UserId, null);
        }

        return await clients.GetAccessibleAsync(viewer, id, cancellationToken).Map(client => new ListScope(viewer.UserId, client.Id));
    }

    /// <summary>
    /// Gets the viewer's time zone: the trainer's or the client's own.
    /// </summary>
    public static async Task<string?> TimeZoneOfAsync(this IClientRepository clients, Viewer viewer, CancellationToken cancellationToken) =>
        viewer.IsTrainer
            ? await clients.GetTrainerTimeZoneAsync(viewer.UserId, cancellationToken)
            : (await clients.GetAsync(viewer.UserId, cancellationToken))?.TimeZone;

    public static async Task<IReadOnlyDictionary<Guid, ExerciseSummary>> GetSummariesAsync(this IExerciseRepository exercises, IEnumerable<Guid> ids, CancellationToken cancellationToken)
    {
        Guid[] distinct = [.. ids.Distinct()];

        if (distinct.Length == 0)
        {
            return new Dictionary<Guid, ExerciseSummary>();
        }

        IReadOnlyList<Exercise> found = await exercises.GetManyAsync(distinct, cancellationToken);
        return found.ToDictionary(e => e.Id, e => e.Summary);
    }

    /// <summary>
    /// Loads a client the viewer may see: a trainer's own client or the client themself. Anything else is not found.
    /// </summary>
    public static Task<Result<ClientProfile>> GetAccessibleAsync(this IClientRepository clients, Viewer viewer, Guid clientId, CancellationToken cancellationToken) =>
        clients.GetAsync(clientId, cancellationToken)
            .ToResult(ClientErrors.NotFound(clientId))
            .Ensure(client => client.IsAccessibleBy(viewer), ClientErrors.NotFound(clientId));
}

/// <summary>
/// The rows a list may return: those of a trainer, of a client, or both when both are set.
/// </summary>
internal sealed record ListScope(Guid? TrainerId, Guid? ClientId);

internal abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    protected PagedQueryValidator(int maxPageSize = TrainingRules.MaxPageSize)
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, maxPageSize);
    }
}
