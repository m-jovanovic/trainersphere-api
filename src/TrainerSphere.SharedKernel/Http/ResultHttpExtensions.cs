using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.SharedKernel.Http;

/// <summary>
/// Maps <see cref="Error"/> to RFC 9457 problem details and <see cref="Result{T}"/> to minimal API results.
/// </summary>
public static class ResultHttpExtensions
{
    /// <summary>
    /// The problem details extension member that carries <see cref="Error.Code"/>.
    /// </summary>
    public const string CodeExtension = "code";

    /// <summary>
    /// Converts the error to problem details. <see cref="ValidationError"/> becomes
    /// <see cref="HttpValidationProblemDetails"/> with an <c>errors</c> member.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The problem details.</returns>
    public static ProblemDetails ToProblemDetails(this Error error)
    {
        ArgumentNullException.ThrowIfNull(error);

        ProblemDetails problem = error is ValidationError validation
            ? new HttpValidationProblemDetails(validation.ToDictionary())
            : new ProblemDetails();

        problem.Status = error.ToStatusCode();
        problem.Title = error.ToTitle();

        // Unexpected errors can carry internals (connection strings, SQL); never echo them to the client.
        problem.Detail = error is UnexpectedError ? "An unexpected error occurred." : error.Message;

        foreach (KeyValuePair<string, object?> extension in error.Extensions ?? new Dictionary<string, object?>())
        {
            problem.Extensions[extension.Key] = extension.Value;
        }

        problem.Extensions[CodeExtension] = error.Code;

        return problem;
    }

    /// <summary>
    /// Gets the HTTP status code for the error.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The status code.</returns>
    // No discard arm: adding an Error kind must fail the build here until it gets a status.
    public static int ToStatusCode(this Error error) =>
        error switch
        {
            ValidationError => StatusCodes.Status400BadRequest,
            DomainError => StatusCodes.Status422UnprocessableEntity,
            UnauthorizedError => StatusCodes.Status401Unauthorized,
            ForbiddenError => StatusCodes.Status403Forbidden,
            NotFoundError => StatusCodes.Status404NotFound,
            ConflictError => StatusCodes.Status409Conflict,
            UnexpectedError => StatusCodes.Status500InternalServerError,
        };

    /// <summary>
    /// Converts the error to a typed problem result.
    /// </summary>
    /// <param name="error">The error.</param>
    /// <returns>The problem result.</returns>
    public static ProblemHttpResult ToProblem(this Error error) => TypedResults.Problem(error.ToProblemDetails());

    /// <summary>
    /// Converts the result to a typed minimal API result, so OpenAPI sees both the success and problem responses.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <typeparam name="TSuccess">The success HTTP result type.</typeparam>
    /// <param name="result">The result.</param>
    /// <param name="onSuccess">Creates the success HTTP result.</param>
    /// <returns>The HTTP result.</returns>
    public static Results<TSuccess, ProblemHttpResult> ToHttpResult<T, TSuccess>(this Result<T> result, Func<T, TSuccess> onSuccess)
        where T : notnull
        where TSuccess : IResult =>
        result.Match<T, Results<TSuccess, ProblemHttpResult>>(value => onSuccess(value), error => error.ToProblem());

    /// <inheritdoc cref="ToHttpResult{T, TSuccess}(Result{T}, Func{T, TSuccess})"/>
    public static async Task<Results<TSuccess, ProblemHttpResult>> ToHttpResult<T, TSuccess>(
        this Task<Result<T>> source,
        Func<T, TSuccess> onSuccess)
        where T : notnull
        where TSuccess : IResult =>
        (await source).ToHttpResult(onSuccess);

    /// <inheritdoc cref="ToHttpResult{T, TSuccess}(Result{T}, Func{T, TSuccess})"/>
    public static async ValueTask<Results<TSuccess, ProblemHttpResult>> ToHttpResult<T, TSuccess>(
        this ValueTask<Result<T>> source,
        Func<T, TSuccess> onSuccess)
        where T : notnull
        where TSuccess : IResult =>
        (await source).ToHttpResult(onSuccess);

    /// <summary>
    /// Converts the result to 200 OK with the value, or a problem.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static Results<Ok<T>, ProblemHttpResult> ToOk<T>(this Result<T> result)
        where T : notnull =>
        result.ToHttpResult(TypedResults.Ok);

    /// <inheritdoc cref="ToOk{T}(Result{T})"/>
    public static async Task<Results<Ok<T>, ProblemHttpResult>> ToOk<T>(this Task<Result<T>> source)
        where T : notnull =>
        (await source).ToOk();

    /// <summary>
    /// Converts the result to 204 No Content, or a problem.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="result">The result.</param>
    /// <returns>The HTTP result.</returns>
    public static Results<NoContent, ProblemHttpResult> ToNoContent<T>(this Result<T> result)
        where T : notnull =>
        result.ToHttpResult(_ => TypedResults.NoContent());

    /// <inheritdoc cref="ToNoContent{T}(Result{T})"/>
    public static async Task<Results<NoContent, ProblemHttpResult>> ToNoContent<T>(this Task<Result<T>> source)
        where T : notnull =>
        (await source).ToNoContent();

    private static string ToTitle(this Error error) =>
        error switch
        {
            ValidationError => "One or more validation errors occurred.",
            DomainError => "Unprocessable Content",
            UnauthorizedError => "Unauthorized",
            ForbiddenError => "Forbidden",
            NotFoundError => "Not Found",
            ConflictError => "Conflict",
            UnexpectedError => "Internal Server Error",
        };
}
