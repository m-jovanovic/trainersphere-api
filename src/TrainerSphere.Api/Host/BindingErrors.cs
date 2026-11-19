using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Diagnostics;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.Api.Host;

/// <summary>
/// Translates minimal API binding failures (<see cref="BadHttpRequestException"/>) into validation errors keyed by
/// camelCase JSON path, so they look like any other 400. A body that cannot be parsed at all is keyed <c>$</c>.
/// </summary>
internal static partial class BindingErrors
{
    public const string BodyField = "$";

    public const string InvalidValue = "The value is not valid.";

    public const string Required = "This field is required.";

    public const string InvalidJson = "The request body is not valid JSON.";

    public const string MissingBody = "A request body is required.";

    public static ValidationError From(BadHttpRequestException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        if (exception.InnerException is JsonException json)
        {
            return FromJson(json);
        }

        // ASP.NET Core only reports binding failures as messages, e.g. Failed to bind parameter "int? page" from "abc".
        Match parameter = ParameterPattern().Match(exception.Message);

        if (!parameter.Success || exception.Message.Contains("body", StringComparison.Ordinal))
        {
            return ValidationError.For(BodyField, exception.InnerException is null ? MissingBody : InvalidJson);
        }

        string field = FieldNames.ToJsonPath(parameter.Groups["name"].Value);
        return ValidationError.For(field, exception.Message.StartsWith("Required parameter", StringComparison.Ordinal) ? Required : InvalidValue);
    }

    public static ValidationError FromJson(JsonException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        string path = ToField(exception.Path);

        if (exception is JsonValueException)
        {
            return ValidationError.For(path, exception.Message);
        }

        // Syntax errors wrap the reader's internal JsonReaderException; type mismatches do not.
        if (exception.InnerException?.GetType().Name == "JsonReaderException")
        {
            return ValidationError.For(BodyField, InvalidJson);
        }

        Match missing = MissingPropertiesPattern().Match(exception.Message);

        if (missing.Success)
        {
            string prefix = path == BodyField ? string.Empty : path + ".";
            return new ValidationError(QuotedNamePattern().Matches(missing.Groups["names"].Value)
                .Select(name => new FieldError(prefix + name.Groups["name"].Value, ValidationError.ValidationCode, Required)));
        }

        return ValidationError.For(path, InvalidValue);
    }

    private static string ToField(string? path) =>
        path switch
        {
            null or "" or "$" => BodyField,
            _ when path.StartsWith("$.", StringComparison.Ordinal) => path[2..],
            _ when path.StartsWith('$', StringComparison.Ordinal) => path[1..],
            _ => path,
        };

    [GeneratedRegex("parameter \"(?:[^\"]*\\s)?(?<name>[^\"\\s]+)\"", RegexOptions.ExplicitCapture)]
    private static partial Regex ParameterPattern();

    [GeneratedRegex("missing required properties including: (?<names>.*)$", RegexOptions.ExplicitCapture | RegexOptions.Singleline)]
    private static partial Regex MissingPropertiesPattern();

    [GeneratedRegex("'(?<name>[^']+)'", RegexOptions.ExplicitCapture)]
    private static partial Regex QuotedNamePattern();
}

/// <summary>
/// Writes binding failures as 400 <c>Validation</c> problems.
/// </summary>
internal sealed class BindingExceptionHandler(IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not BadHttpRequestException { StatusCode: StatusCodes.Status400BadRequest } badRequest)
        {
            return false;
        }

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;

        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = BindingErrors.From(badRequest).ToProblemDetails(),
            Exception = exception,
        });
    }
}
