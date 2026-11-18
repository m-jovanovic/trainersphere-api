using Microsoft.AspNetCore.Http;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.SharedKernel.Http;

/// <summary>
/// Maps errors to RFC 9457 problem details. Every problem carries the stable error <c>code</c> as an extension.
/// </summary>
public static class ProblemMapping
{
    public const string CodeExtension = "code";

    public static int StatusCodeFor(Error error) => error switch
    {
        ValidationError => StatusCodes.Status400BadRequest,
        UnauthorizedError => StatusCodes.Status401Unauthorized,
        ForbiddenError => StatusCodes.Status403Forbidden,
        NotFoundError => StatusCodes.Status404NotFound,
        ConflictError => StatusCodes.Status409Conflict,
        DomainError => StatusCodes.Status422UnprocessableEntity,
        UnexpectedError => StatusCodes.Status500InternalServerError,
    };

    public static IResult ToProblem(this Error error, IReadOnlyDictionary<string, object?>? extensions = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        Dictionary<string, object?> ext = new(StringComparer.Ordinal) { [CodeExtension] = error.Code };

        if (extensions is not null)
        {
            foreach (KeyValuePair<string, object?> pair in extensions)
            {
                ext[pair.Key] = pair.Value;
            }
        }

        if (error is ValidationError validation)
        {
            var fields = validation.Errors
                .GroupBy(e => e.Field, StringComparer.Ordinal)
                .ToDictionary(g => g.Key, g => g.Select(e => e.Message).ToArray(), StringComparer.Ordinal);

            return TypedResults.ValidationProblem(fields, detail: "The request is invalid.", extensions: ext);
        }

        return TypedResults.Problem(
            detail: error is UnexpectedError ? "An unexpected error occurred." : error.Message,
            statusCode: StatusCodeFor(error),
            extensions: ext);
    }

    public static IResult ToHttpResult<T>(this Result<T> result, Func<T, IResult> onSuccess)
        where T : notnull =>
        result.Match(onSuccess, static error => error.ToProblem());

    public static IResult ToOk<T>(this Result<T> result)
        where T : notnull =>
        result.Match(static value => (IResult)TypedResults.Ok(value), static error => error.ToProblem());

    public static IResult ToNoContent<T>(this Result<T> result)
        where T : notnull =>
        result.Match(static _ => (IResult)TypedResults.NoContent(), static error => error.ToProblem());
}
