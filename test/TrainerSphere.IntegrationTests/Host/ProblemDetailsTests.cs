using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// The API host plus one route that throws, so the unhandled-exception path runs through the real middleware.
/// </summary>
public sealed class FailingRouteApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    public const string Route = "/test/throw";
    public const string Secret = "Host=db;Password=hunter2";

    protected override void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IStartupFilter>(new ThrowingRouteStartupFilter());

    // Mapped after the application pipeline, so it runs inside the API's exception handler like any endpoint.
    private sealed class ThrowingRouteStartupFilter : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
        {
            next(app);
            app.Map(Route, branch => branch.Run(_ => throw new InvalidOperationException($"Connection failed: {Secret}")));
        };
    }
}

public sealed partial class ProblemDetailsTests(FailingRouteApiFactory factory) : IntegrationTest(factory), IClassFixture<FailingRouteApiFactory>
{
    [Fact]
    public async Task UnknownRoute_Returns404NotFoundProblem()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/api/does-not-exist", Ct);

        Problem problem = await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "NotFound");
        problem.Instance.ShouldBe("/api/does-not-exist");
    }

    [Fact]
    public async Task UnknownRoute_Returns404ProblemWithEveryMember()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/api/does-not-exist", Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.NotFound, "NotFound");
    }

    [Fact]
    public async Task WrongMethod_Returns405MethodNotAllowedProblem()
    {
        HttpResponseMessage response = await CreateAnonymousClient().DeleteAsync(IdentityRoutes.Login, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.MethodNotAllowed, "MethodNotAllowed");
    }

    [Fact]
    public async Task WrongMethod_Returns405ProblemWithEveryMember()
    {
        HttpResponseMessage response = await CreateAnonymousClient().DeleteAsync(IdentityRoutes.Login, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.MethodNotAllowed, "MethodNotAllowed");
    }

    [Fact]
    public async Task Anonymous_OnProtectedRoute_Returns401ProblemWithEveryMember()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(IdentityRoutes.Me, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task GarbageAccessToken_Returns401UnauthorizedProblem()
    {
        HttpResponseMessage response = await Factory.CreateBearerClient("not-a-jwt").GetAsync(IdentityRoutes.Me, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task GarbageAccessToken_Returns401ProblemWithEveryMember()
    {
        HttpResponseMessage response = await Factory.CreateBearerClient("not-a-jwt").GetAsync(IdentityRoutes.Me, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Client_OnTrainerRoute_Returns403ProblemWithEveryMember()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await clients[0].Client.GetAsync(IdentityRoutes.Invitations, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task DuplicateSignup_Returns409ProblemWithEveryMember()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Signup, new SignupBuilder().WithEmail(trainer.Email).Build());

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Fact]
    public async Task InvalidBody_Returns400ProblemWithEveryMemberAndErrors()
    {
        HttpResponseMessage response = await CreateAnonymousClient().PostJsonAsync(
            IdentityRoutes.Signup,
            new SignupBuilder().WithEmail("not-an-email").Build());

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.GetProperty("errors").GetProperty("email").EnumerateArray().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task UnhandledException_Returns500ServerErrorProblem()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(FailingRouteApiFactory.Route, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.InternalServerError, "Server.Error");
    }

    [Fact]
    public async Task UnhandledException_Returns500ProblemWithGenericDetail()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(FailingRouteApiFactory.Route, Ct);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.InternalServerError, "Server.Error");
        problem.GetProperty("detail").GetString().ShouldBe("An unexpected error occurred.");
    }

    [Fact]
    public async Task UnhandledException_DoesNotLeakInternals()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(FailingRouteApiFactory.Route, Ct);

        string body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("hunter2");
        body.ShouldNotContain(nameof(InvalidOperationException));
        body.ShouldNotContain("   at ");
    }

    [Fact]
    public async Task Problem_TraceId_IsAW3cTraceparent()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/api/does-not-exist", Ct);

        string? traceId = (await response.ReadJsonAsync()).GetProperty("traceId").GetString();
        traceId.ShouldNotBeNull().ShouldMatch(Traceparent().ToString());
    }

    [Fact]
    public async Task Problem_TraceId_DiffersPerRequest()
    {
        HttpClient client = CreateAnonymousClient();

        string? first = (await (await client.GetAsync("/api/does-not-exist", Ct)).ReadJsonAsync()).GetProperty("traceId").GetString();
        string? second = (await (await client.GetAsync("/api/does-not-exist", Ct)).ReadJsonAsync()).GetProperty("traceId").GetString();

        first.ShouldNotBe(second);
    }

    [GeneratedRegex("^00-[0-9a-f]{32}-[0-9a-f]{16}-[0-9a-f]{2}$")]
    private static partial Regex Traceparent();
}
