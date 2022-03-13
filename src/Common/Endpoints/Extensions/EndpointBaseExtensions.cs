using Ardalis.ApiEndpoints;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Shared.Results;

namespace Endpoints.Extensions;

/// <summary>
/// Contains extension methods for the <see cref="EndpointBase"/> class.
/// </summary>
public static class EndpointBaseExtensions
{
    /// <summary>
    /// Handles the failure result and returns the appropriate response.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="result">The failure result.</param>
    /// <returns>The appropriate response based on the result type.</returns>
    /// <exception cref="InvalidOperationException"> when this method is invoked with a success result.</exception>
    public static ActionResult HandleFailure(this EndpointBase endpoint, Result result) =>
        result switch
        {
            { IsSuccess: true } => throw new InvalidOperationException("This method should not be invoked for a success result."),
            IValidationResult validationResult => endpoint.BadRequest(new ProblemDetails
            {
                Type = "Validation Error",
                Title = "Validation failure occurred",
                Detail = "There was a problem validating your request.",
                Status = StatusCodes.Status400BadRequest,
                Extensions = { { "errors", validationResult.Errors } }
            }),
            var notFound when notFound.Error.Code.Contains("NotFound", StringComparison.InvariantCulture) =>
                endpoint.Conflict(CreateProblemDetails("Not Found", StatusCodes.Status404NotFound, notFound.Error)),
            var conflict when conflict.Error.Code.Contains("Exists", StringComparison.InvariantCulture) =>
                endpoint.Conflict(CreateProblemDetails("Conflict", StatusCodes.Status409Conflict, conflict.Error)),
            _ => endpoint.BadRequest(new
            {
                Errors = new[]
                {
                    result.Error
                }
            })
        };

    private static ProblemDetails CreateProblemDetails(string type, int status, Error error) =>
        new()
        {
            Type = type,
            Title = error.Code,
            Detail = error.Message,
            Status = status
        };
}
