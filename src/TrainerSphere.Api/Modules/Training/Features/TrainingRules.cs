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

internal abstract class PagedQueryValidator<T> : AbstractValidator<T>
    where T : IPagedQuery
{
    protected PagedQueryValidator(int maxPageSize = TrainingRules.MaxPageSize)
    {
        RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
        RuleFor(q => q.PageSize).InclusiveBetween(1, maxPageSize);
    }
}
