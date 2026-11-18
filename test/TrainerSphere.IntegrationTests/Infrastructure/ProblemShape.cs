using System.Net;
using System.Text.Json;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// The full RFC 9457 shape from section 3 of the contract: every error, whichever layer produced it (handler,
/// authentication, routing, rate limiter, exception handler), carries the same members.
/// </summary>
public static class ProblemShape
{
    private static readonly string[] TextMembers = ["type", "title", "detail", "instance", "code", "traceId"];

    public static async Task<JsonElement> ShouldBeCompleteProblemAsync(this HttpResponseMessage response, HttpStatusCode expected, string code)
    {
        ArgumentNullException.ThrowIfNull(response);

        await response.ShouldHaveStatusAsync(expected);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        JsonElement problem = await response.ReadJsonAsync();

        foreach (string member in TextMembers)
        {
            problem.TryGetProperty(member, out JsonElement value).ShouldBeTrue($"Problem member '{member}' is missing: {problem}");
            value.ValueKind.ShouldBe(JsonValueKind.String, $"Problem member '{member}' must be a string: {problem}");
            value.GetString().ShouldNotBeNullOrWhiteSpace($"Problem member '{member}' is empty: {problem}");
        }

        problem.GetProperty("status").GetInt32().ShouldBe((int)expected);
        problem.GetProperty("code").GetString().ShouldBe(code);
        problem.GetProperty("instance").GetString().ShouldBe(response.RequestMessage?.RequestUri?.AbsolutePath);
        return problem;
    }
}
