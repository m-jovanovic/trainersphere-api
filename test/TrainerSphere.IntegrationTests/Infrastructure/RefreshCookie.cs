using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// A parsed <c>Set-Cookie</c> response header. Attribute names are case-insensitive; flags such as
/// <c>HttpOnly</c> have a null value.
/// </summary>
public sealed record SetCookie(string Name, string Value, IReadOnlyDictionary<string, string?> Attributes)
{
    public DateTimeOffset? Expires =>
        Attributes.TryGetValue("expires", out string? value) && value is not null
            ? DateTimeOffset.Parse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal)
            : null;

    public static SetCookie Parse(string header)
    {
        ArgumentNullException.ThrowIfNull(header);

        string[] parts = header.Split(';', StringSplitOptions.TrimEntries);
        int separator = parts[0].IndexOf('=', StringComparison.Ordinal);
        Dictionary<string, string?> attributes = new(StringComparer.OrdinalIgnoreCase);

        foreach (string part in parts.Skip(1).Where(p => p.Length > 0))
        {
            int equals = part.IndexOf('=', StringComparison.Ordinal);
            attributes[equals < 0 ? part : part[..equals].Trim()] = equals < 0 ? null : part[(equals + 1)..].Trim();
        }

        return new SetCookie(parts[0][..separator], parts[0][(separator + 1)..], attributes);
    }

    public bool Has(string attribute) => Attributes.ContainsKey(attribute);

    public string? Attribute(string name) => Attributes.TryGetValue(name, out string? value) ? value : null;
}

/// <summary>
/// The refresh token cookie as the contract defines it (section 4): <c>ts_refresh</c>, HttpOnly, Secure,
/// SameSite=Strict, Path=/api/auth, 14 days.
/// </summary>
public static class RefreshCookie
{
    public const string Name = "ts_refresh";

    public static readonly TimeSpan Lifetime = TimeSpan.FromDays(14);

    public static SetCookie? GetSetCookie(this HttpResponseMessage response, string name)
    {
        ArgumentNullException.ThrowIfNull(response);

        return response.Headers.TryGetValues("Set-Cookie", out IEnumerable<string>? values)
            ? values.Select(SetCookie.Parse).LastOrDefault(c => c.Name == name)
            : null;
    }

    /// <summary>
    /// Asserts the response issues a fresh refresh cookie with every contract attribute and returns the token.
    /// </summary>
    public static string ShouldSetRefreshCookie(this HttpResponseMessage response, bool secure = true)
    {
        SetCookie cookie = response.GetSetCookie(Name).ShouldNotBeNull($"Expected a Set-Cookie for {Name}.");

        cookie.Value.Length.ShouldBe(43, "The refresh token is a 256-bit URL-safe base64 value.");
        cookie.Has("httponly").ShouldBeTrue("The refresh cookie must be HttpOnly.");
        cookie.Has("secure").ShouldBe(secure);
        cookie.Attribute("samesite").ShouldBe("strict", StringCompareShould.IgnoreCase);
        cookie.Attribute("path").ShouldBe("/api/auth");
        cookie.Expires.ShouldNotBeNull().ShouldBeInRange(
            DateTimeOffset.UtcNow + Lifetime - TimeSpan.FromMinutes(2),
            DateTimeOffset.UtcNow + Lifetime + TimeSpan.FromMinutes(2));

        return cookie.Value;
    }

    /// <summary>
    /// Asserts the response deletes the refresh cookie (empty value, expiry in the past, same path).
    /// </summary>
    public static void ShouldClearRefreshCookie(this HttpResponseMessage response)
    {
        SetCookie cookie = response.GetSetCookie(Name).ShouldNotBeNull($"Expected a Set-Cookie that clears {Name}.");

        cookie.Value.ShouldBeEmpty();
        cookie.Expires.ShouldNotBeNull().ShouldBeLessThan(DateTimeOffset.UtcNow);
        cookie.Attribute("path").ShouldBe("/api/auth");
    }

    public static void ShouldNotSetRefreshCookie(this HttpResponseMessage response) =>
        response.GetSetCookie(Name).ShouldBeNull();

    /// <summary>
    /// A client without a cookie jar, for replaying a specific (for example an already rotated) refresh token.
    /// </summary>
    public static HttpClient CreateCookielessClient(this ApiFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);

        return factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false,
            AllowAutoRedirect = false,
        });
    }

    /// <summary>
    /// POSTs to <paramref name="path"/> sending exactly <paramref name="token"/> as the refresh cookie (none when null).
    /// </summary>
    public static async Task<HttpResponseMessage> PostWithRefreshCookieAsync(this ApiFactory factory, string path, string? token, string? accessToken = null)
    {
        HttpClient client = factory.CreateCookielessClient();
        using HttpRequestMessage request = new(HttpMethod.Post, path);

        if (token is not null)
        {
            request.Headers.Add("Cookie", $"{Name}={token}");
        }

        if (accessToken is not null)
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
        }

        return await client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<HttpResponseMessage> RefreshWithCookieAsync(this ApiFactory factory, string? token) =>
        factory.PostWithRefreshCookieAsync("/api/auth/refresh", token);

    public static Task<HttpStatusCode> RefreshStatusAsync(this ApiFactory factory, string token) =>
        factory.RefreshWithCookieAsync(token).ContinueWith(t => t.Result.StatusCode, TaskScheduler.Default);
}
