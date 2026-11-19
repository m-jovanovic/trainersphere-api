using System.Net;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Section 15: the API allows the Vite dev origin with credentials (the refresh cookie), and no other origin.
/// </summary>
public sealed class CorsTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string WebOrigin = "http://localhost:5173";
    private const string OtherOrigin = "http://evil.test";

    [Theory]
    [InlineData("POST", IdentityRoutes.Login)]
    [InlineData("POST", IdentityRoutes.Refresh)]
    [InlineData("PUT", IdentityRoutes.Me)]
    [InlineData("DELETE", "/api/invitations/0193f1b0-1111-7aaa-8bbb-000000000001")]
    public async Task Preflight_FromWebOrigin_AllowsCredentialedRequest(string method, string route)
    {
        HttpResponseMessage response = await PreflightAsync(WebOrigin, method, route);

        response.IsSuccessStatusCode.ShouldBeTrue($"Preflight returned {(int)response.StatusCode}.");
        Header(response, "Access-Control-Allow-Origin").ShouldBe(WebOrigin);
        Header(response, "Access-Control-Allow-Credentials").ShouldBe("true");
        Header(response, "Access-Control-Allow-Methods").ShouldNotBeNull().ShouldContain(method, Case.Insensitive);
        string allowedHeaders = Header(response, "Access-Control-Allow-Headers").ShouldNotBeNull();
        allowedHeaders.ShouldContain("content-type", Case.Insensitive);
        allowedHeaders.ShouldContain("authorization", Case.Insensitive);
    }

    [Fact]
    public async Task Preflight_FromOtherOrigin_IsNotAllowed()
    {
        HttpResponseMessage response = await PreflightAsync(OtherOrigin, "POST", IdentityRoutes.Login);

        Header(response, "Access-Control-Allow-Origin").ShouldBeNull();
        Header(response, "Access-Control-Allow-Credentials").ShouldBeNull();
    }

    [Fact]
    public async Task Login_FromWebOrigin_AllowsTheOriginWithCredentials()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await SendAsync(WebOrigin, HttpMethod.Post, IdentityRoutes.Login, JsonContent(new LoginRequest(trainer.Email, trainer.Password)));

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(WebOrigin);
        Header(response, "Access-Control-Allow-Credentials").ShouldBe("true");
        response.ShouldSetRefreshCookie();
    }

    [Fact]
    public async Task ErrorResponse_FromWebOrigin_IsReadableByTheBrowser()
    {
        HttpResponseMessage response = await SendAsync(WebOrigin, HttpMethod.Get, IdentityRoutes.Me);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
        Header(response, "Access-Control-Allow-Origin").ShouldBe(WebOrigin);
        Header(response, "Access-Control-Allow-Credentials").ShouldBe("true");
    }

    [Fact]
    public async Task Request_FromOtherOrigin_HasNoCorsHeaders()
    {
        HttpResponseMessage response = await SendAsync(OtherOrigin, HttpMethod.Get, "/health/live");

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        Header(response, "Access-Control-Allow-Origin").ShouldBeNull();
    }

    private static string? Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out IEnumerable<string>? values) ? string.Join(", ", values) : null;

    private static StringContent JsonContent<T>(T body) =>
        new(System.Text.Json.JsonSerializer.Serialize(body, HttpAssertions.Json), System.Text.Encoding.UTF8, "application/json");

    private Task<HttpResponseMessage> PreflightAsync(string origin, string method, string route)
    {
        HttpRequestMessage request = new(HttpMethod.Options, route);
        request.Headers.Add("Origin", origin);
        request.Headers.Add("Access-Control-Request-Method", method);
        request.Headers.Add("Access-Control-Request-Headers", "content-type,authorization");
        return CreateAnonymousClient().SendAsync(request, Ct);
    }

    private Task<HttpResponseMessage> SendAsync(string origin, HttpMethod method, string route, HttpContent? content = null)
    {
        HttpRequestMessage request = new(method, route) { Content = content };
        request.Headers.Add("Origin", origin);
        return CreateAnonymousClient().SendAsync(request, Ct);
    }
}
