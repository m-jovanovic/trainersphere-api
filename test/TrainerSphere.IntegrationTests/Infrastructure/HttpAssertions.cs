using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;

namespace TrainerSphere.IntegrationTests.Infrastructure;

public static class HttpAssertions
{
    /// <summary>
    /// The wire format: camelCase properties, enums as kebab-case strings. Test DTOs keep enums as strings so the
    /// tests assert the exact values the contract documents.
    /// </summary>
    public static JsonSerializerOptions Json { get; } = new(JsonSerializerDefaults.Web);

    public static async Task<HttpResponseMessage> ShouldHaveStatusAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        ArgumentNullException.ThrowIfNull(response);

        if (response.StatusCode != expected)
        {
            string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            throw new ShouldAssertException(
                $"Expected {(int)expected} {expected} from {response.RequestMessage?.Method} {response.RequestMessage?.RequestUri?.PathAndQuery} " +
                $"but got {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{body}");
        }

        return response;
    }

    public static async Task<T> ShouldHaveJsonAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
        where T : class
    {
        await response.ShouldHaveStatusAsync(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        T? body = await response.Content.ReadFromJsonAsync<T>(Json, TestContext.Current.CancellationToken);
        return body.ShouldNotBeNull();
    }

    /// <summary>
    /// Asserts an RFC 9457 problem response with the given status and machine-readable <c>code</c>.
    /// </summary>
    public static async Task<Problem> ShouldBeProblemAsync(this HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        await response.ShouldHaveStatusAsync(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");

        Problem? problem = await response.Content.ReadFromJsonAsync<Problem>(Json, TestContext.Current.CancellationToken);
        problem.ShouldNotBeNull();
        problem.Status.ShouldBe((int)expected);
        problem.Code.ShouldBe(code);
        problem.TraceId.ShouldNotBeNullOrWhiteSpace();
        return problem;
    }

    /// <summary>
    /// Asserts a 400 <c>Validation</c> problem that has an error for every given JSON path (for example <c>sets[2].reps</c>).
    /// </summary>
    public static async Task<Problem> ShouldBeValidationProblemAsync(this HttpResponseMessage response, params string[] fields)
    {
        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.Errors.ShouldNotBeNull();

        foreach (string field in fields)
        {
            problem.Errors.ShouldContainKey(field);
            problem.Errors[field].ShouldNotBeEmpty();
        }

        return problem;
    }

    public static string? GetCookie(this HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.FirstOrDefault(v => v.StartsWith(name + "=", StringComparison.Ordinal))
            : null;
}
