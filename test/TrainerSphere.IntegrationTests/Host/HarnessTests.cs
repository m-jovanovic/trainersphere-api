using System.Net;
using System.Net.Http.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Proves the harness itself: per-role users, cookies, problem details and message draining.
/// </summary>
public sealed class HarnessTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateTrainer_ReturnsSignedInTrainer()
    {
        TestUser trainer = await CreateTrainerAsync();

        User me = await (await trainer.Client.GetAsync("/api/auth/me", Ct)).ShouldHaveJsonAsync<User>();

        me.Id.ShouldBe(trainer.Id);
        me.Role.ShouldBe("trainer");
        me.TrainerId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateClient_ReturnsSignedInClientOfTheTrainer()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync(2);

        foreach (TestUser client in clients)
        {
            User me = await (await client.Client.GetAsync("/api/auth/me", Ct)).ShouldHaveJsonAsync<User>();
            me.Role.ShouldBe("client");
            me.TrainerId.ShouldBe(trainer.Id);
        }
    }

    [Fact]
    public async Task UserClient_KeepsRefreshCookie()
    {
        TestUser trainer = await CreateTrainerAsync();

        AuthResponse refreshed = await (await trainer.Client.PostAsync("/api/auth/refresh", content: null, Ct)).ShouldHaveJsonAsync<AuthResponse>();

        refreshed.User.Id.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task Anonymous_OnProtectedRoute_GetsUnauthorizedProblem()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync("/api/auth/me", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task Client_OnTrainerRoute_GetsForbiddenProblem()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await clients[0].Client.GetAsync("/api/invitations", Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task InvalidBody_GetsValidationProblemWithCamelCaseKeys()
    {
        HttpResponseMessage response = await CreateAnonymousClient().PostAsJsonAsync(
            "/api/auth/signup",
            new SignupRequest("not-an-email", "short", string.Empty, "Novak", "Not/AZone"),
            HttpAssertions.Json,
            Ct);

        await response.ShouldBeValidationProblemAsync("email", "password", "firstName", "timeZone");
    }

    [Fact]
    public Task WaitForMessages_WhenNothingPending_ReturnsImmediately() =>
        Should.NotThrowAsync(() => Factory.WaitForMessagesAsync(TimeSpan.FromSeconds(1)));
}
