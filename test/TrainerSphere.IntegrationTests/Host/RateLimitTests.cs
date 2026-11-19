using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// The host with a low auth rate limit, where each client can claim its own remote IP address.
/// </summary>
public sealed class RateLimitedApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    public const int PermitLimit = 3;
    private const string ClientIpHeader = "X-Test-Client-Ip";

    /// <summary>
    /// An anonymous client whose requests arrive from a fresh IP address, so its rate limit partition is its own.
    /// </summary>
    public HttpClient CreateClientFromNewAddress()
    {
        HttpClient client = CreateAnonymousClient();
        client.DefaultRequestHeaders.Add(ClientIpHeader, new IPAddress(Guid.NewGuid().ToByteArray()).ToString());
        return client;
    }

    protected override void ConfigureSettings(IDictionary<string, string?> settings) =>
        settings["RateLimiting:AuthPermitLimitPerMinute"] = PermitLimit.ToString(CultureInfo.InvariantCulture);

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IStartupFilter>(new ClientIpStartupFilter());

    // TestServer has no remote address, so without this every request would share one rate limit partition.
    private sealed class ClientIpStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            app.Use((context, nextMiddleware) =>
            {
                if (IPAddress.TryParse(context.Request.Headers[ClientIpHeader].ToString(), out IPAddress? address))
                {
                    context.Connection.RemoteIpAddress = address;
                }

                return nextMiddleware(context);
            });
            next(app);
        };
    }
}

/// <summary>
/// Section 3: auth endpoints are rate limited per IP and answer 429 <c>RateLimited</c> when over the limit.
/// </summary>
public sealed class RateLimitTests(RateLimitedApiFactory factory) : IntegrationTest(factory), IClassFixture<RateLimitedApiFactory>
{
    private readonly RateLimitedApiFactory _limited = factory;

    [Theory]
    [InlineData(IdentityRoutes.Login)]
    [InlineData(IdentityRoutes.Signup)]
    [InlineData(IdentityRoutes.ForgotPassword)]
    public async Task AuthEndpoint_OverTheLimit_Returns429RateLimited(string route)
    {
        HttpClient client = _limited.CreateClientFromNewAddress();

        for (int i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            (await PostAsync(client, route)).StatusCode.ShouldNotBe(HttpStatusCode.TooManyRequests, $"Request {i + 1} is within the limit.");
        }

        HttpResponseMessage response = await PostAsync(client, route);

        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.TooManyRequests, "RateLimited");
        problem.Detail.ShouldBe("Too many attempts. Try again in a minute.");
    }

    [Fact]
    public async Task Login_OverTheLimit_Returns429ProblemWithEveryMember()
    {
        HttpClient client = _limited.CreateClientFromNewAddress();

        for (int i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            await PostAsync(client, IdentityRoutes.Login);
        }

        HttpResponseMessage response = await PostAsync(client, IdentityRoutes.Login);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.TooManyRequests, "RateLimited");
    }

    [Fact]
    public async Task Login_FromAnotherAddress_IsNotLimitedByTheFirst()
    {
        HttpClient limited = _limited.CreateClientFromNewAddress();

        for (int i = 0; i <= RateLimitedApiFactory.PermitLimit; i++)
        {
            await PostAsync(limited, IdentityRoutes.Login);
        }

        HttpResponseMessage response = await PostAsync(_limited.CreateClientFromNewAddress(), IdentityRoutes.Login);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    [Fact]
    public async Task OverTheAuthLimit_OtherEndpoints_StillAnswer()
    {
        HttpClient client = _limited.CreateClientFromNewAddress();
        AuthResponse auth = await (await client.PostJsonAsync(IdentityRoutes.Signup, new SignupBuilder().Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        for (int i = 0; i < RateLimitedApiFactory.PermitLimit; i++)
        {
            await PostAsync(client, IdentityRoutes.Login);
        }

        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.AccessToken);

        for (int i = 0; i <= RateLimitedApiFactory.PermitLimit; i++)
        {
            await (await client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        }
    }

    private static Task<HttpResponseMessage> PostAsync(HttpClient client, string route) => route switch
    {
        IdentityRoutes.Login => client.PostJsonAsync(route, new LoginRequest(Unique.Email("limited"), "Wrong-pass-123")),
        IdentityRoutes.Signup => client.PostJsonAsync(route, new SignupBuilder().Build()),
        IdentityRoutes.ForgotPassword => client.PostJsonAsync(route, new ForgotPasswordRequest(Unique.Email("limited"))),
        _ => throw new ArgumentOutOfRangeException(nameof(route), route, "No request body for this route."),
    };
}
