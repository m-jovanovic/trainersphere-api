using System.Net;
using System.Net.Http.Json;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

/// <summary>
/// A sign-in captured with its raw refresh token, for tests that replay, rotate or revoke specific tokens.
/// </summary>
internal sealed record SignedIn(User User, string Password, string AccessToken, string RefreshToken);

internal static class AuthFlows
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task<SignedIn> SignupAsync(this ApiFactory factory, Action<SignupBuilder>? configure = null)
    {
        SignupBuilder builder = new();
        configure?.Invoke(builder);
        SignupRequest request = builder.Build();

        HttpResponseMessage response = await factory.CreateAnonymousClient().PostAsJsonAsync(IdentityRoutes.Signup, request, HttpAssertions.Json, Ct);
        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        return new SignedIn(auth.User, request.Password, auth.AccessToken, response.ShouldSetRefreshCookie());
    }

    public static async Task<SignedIn> LoginAsync(this ApiFactory factory, string email, string password)
    {
        HttpResponseMessage response = await factory.CreateAnonymousClient().PostAsJsonAsync(IdentityRoutes.Login, new LoginRequest(email, password), HttpAssertions.Json, Ct);
        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>();

        return new SignedIn(auth.User, password, auth.AccessToken, response.ShouldSetRefreshCookie());
    }

    /// <summary>
    /// Rotates <paramref name="refreshToken"/> and returns the new token.
    /// </summary>
    public static async Task<string> RotateAsync(this ApiFactory factory, string refreshToken)
    {
        HttpResponseMessage response = await factory.RefreshWithCookieAsync(refreshToken);
        return (await response.ShouldHaveStatusAsync(HttpStatusCode.OK)).ShouldSetRefreshCookie();
    }

    public static Task<HttpResponseMessage> PostJsonAsync<T>(this HttpClient client, string route, T body) =>
        client.PostAsJsonAsync(route, body, HttpAssertions.Json, Ct);

    public static Task<HttpResponseMessage> PutJsonAsync<T>(this HttpClient client, string route, T body) =>
        client.PutAsJsonAsync(route, body, HttpAssertions.Json, Ct);

    /// <summary>
    /// Sends a raw JSON body, for payloads the typed contract records cannot express (missing members, wrong types).
    /// </summary>
    public static Task<HttpResponseMessage> SendRawJsonAsync(this HttpClient client, HttpMethod method, string route, string json)
    {
        HttpRequestMessage request = new(method, route) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") };
        return client.SendAsync(request, Ct);
    }
}
