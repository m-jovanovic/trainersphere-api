using Ardalis.ApiEndpoints;
using Domain.Results;
using Microsoft.AspNetCore.Mvc;

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
            IValidationResult validationResult => endpoint.BadRequest(new
            {
                validationResult.Errors
            }),
            var notFound when notFound.Error.Code.EndsWith("NotFound", StringComparison.InvariantCulture) => endpoint.NotFound(new
            {
                Errors = new[]
                {
                    result.Error
                }
            }),
            _ => endpoint.BadRequest(new
            {
                Errors = new[]
                {
                    result.Error
                }
            })
        };
}
