using System.Net;
using System.Net.Http.Json;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Base class for API tests. Each test class declares <c>IClassFixture&lt;ApiFactory&gt;</c> (or a subclass with other
/// settings) and so gets its own host plus freshly migrated database, shared by its tests; tests therefore create
/// their own users and data with unique values and never depend on each other's rows.
/// </summary>
public abstract class IntegrationTest(ApiFactory factory)
{
    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    protected ApiFactory Factory { get; } = factory;

    protected HttpClient CreateAnonymousClient() => Factory.CreateAnonymousClient();

    /// <summary>
    /// Signs up a trainer through the API. The returned user's client sends the bearer token and holds the refresh cookie.
    /// </summary>
    protected async Task<TestUser> CreateTrainerAsync(Action<SignupBuilder>? configure = null)
    {
        SignupBuilder builder = new();
        configure?.Invoke(builder);
        SignupRequest request = builder.Build();

        HttpClient client = CreateAnonymousClient();
        AuthResponse auth = await (await client.PostAsJsonAsync("/api/auth/signup", request, HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        return TestUser.From(client, auth, request.Password);
    }

    /// <summary>
    /// Invites a client as the trainer and returns the invitation with the raw link token (normally only sent by email).
    /// </summary>
    protected async Task<IssuedInvitation> InviteAsync(TestUser trainer, Action<InvitationBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(trainer);

        InvitationBuilder builder = new();
        configure?.Invoke(builder);
        CreateInvitationRequest request = builder.Build();

        Invitation invitation = await (await trainer.Client.PostAsJsonAsync("/api/invitations", request, HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<Invitation>(HttpStatusCode.Created);

        string token = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == invitation.Id).Token;
        return new IssuedInvitation(invitation, token);
    }

    /// <summary>
    /// Creates a client the way production does: the trainer invites, the client accepts the link. Waits for the
    /// asynchronous follow-up (Training profile) so the client is fully usable.
    /// </summary>
    protected async Task<TestUser> CreateClientAsync(TestUser trainer, Action<AcceptInvitationBuilder>? configure = null)
    {
        IssuedInvitation issued = await InviteAsync(trainer);

        AcceptInvitationBuilder builder = new AcceptInvitationBuilder()
            .WithFirstName(issued.Invitation.FirstName)
            .WithLastName(issued.Invitation.LastName);
        configure?.Invoke(builder);
        AcceptInvitationRequest request = builder.Build();

        HttpClient client = CreateAnonymousClient();
        AuthResponse auth = await (await client.PostAsJsonAsync($"/api/invitations/{issued.Token}/accept", request, HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        await Factory.WaitForMessagesAsync();
        return TestUser.From(client, auth, request.Password);
    }

    /// <summary>
    /// A trainer with <paramref name="clients"/> accepted clients.
    /// </summary>
    protected async Task<(TestUser Trainer, IReadOnlyList<TestUser> Clients)> CreateTrainerWithClientsAsync(int clients = 1)
    {
        TestUser trainer = await CreateTrainerAsync();
        List<TestUser> created = [];

        for (int i = 0; i < clients; i++)
        {
            created.Add(await CreateClientAsync(trainer));
        }

        return (trainer, created);
    }

    /// <summary>
    /// Logs in through the API with a new client (new cookie jar, new refresh token family).
    /// </summary>
    protected async Task<TestUser> LoginAsync(string email, string password)
    {
        HttpClient client = CreateAnonymousClient();
        AuthResponse auth = await (await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), HttpAssertions.Json, Ct))
            .ShouldHaveJsonAsync<AuthResponse>();

        return TestUser.From(client, auth, password);
    }
}

public sealed record IssuedInvitation(Invitation Invitation, string Token);
