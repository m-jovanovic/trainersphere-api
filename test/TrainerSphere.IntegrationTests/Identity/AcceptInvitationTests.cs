using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class AcceptInvitationTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    public static TheoryData<string> InvalidNames => [string.Empty, "   ", new string('n', 101)];

    [Fact]
    public async Task AcceptInvitation_WhenPending_Returns201AuthResponseForNewClient()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        AcceptInvitationRequest request = new AcceptInvitationBuilder().WithFirstName("Alex").WithLastName("Rivera").WithTimeZone("Europe/Belgrade").Build();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-5);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token, request);

        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(IdentityRoutes.Me);
        auth.TokenType.ShouldBe("Bearer");
        auth.ExpiresIn.ShouldBe(900);
        auth.User.Id.ShouldNotBe(Guid.Empty);
        auth.User.Id.ShouldNotBe(trainer.Id);
        auth.User.Email.ShouldBe(issued.Invitation.Email);
        auth.User.FirstName.ShouldBe("Alex");
        auth.User.LastName.ShouldBe("Rivera");
        auth.User.Role.ShouldBe("client");
        auth.User.TrainerId.ShouldBe(trainer.Id);
        auth.User.Units.ShouldBe("kg");
        auth.User.TimeZone.ShouldBe("Europe/Belgrade");
        auth.User.CreatedAt.ShouldBeInRange(before, DateTimeOffset.UtcNow.AddSeconds(5));
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.AuthResponseProperties);
        body.GetProperty("user").ShouldHaveExactProperties(IdentityRoutes.UserProperties);
    }

    [Fact]
    public async Task AcceptInvitation_WhenPending_SetsRefreshCookie()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token);

        string token = (await response.ShouldHaveStatusAsync(HttpStatusCode.Created)).ShouldSetRefreshCookie();
        await Factory.RotateAsync(token);
    }

    [Fact]
    public async Task AcceptInvitation_WhenPending_IssuesClientAccessToken()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithFirstName("Alex").WithLastName("Rivera").Build()))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Claim("sub").ShouldBe(auth.User.Id.ToString());
        token.Claim("email").ShouldBe(issued.Invitation.Email);
        token.Claim("role").ShouldBe("client");
        token.Claim("name").ShouldBe("Alex Rivera");
        token.Claim("trainer_id").ShouldBe(trainer.Id.ToString());
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromMinutes(15));
        (await (await Factory.CreateBearerClient(auth.AccessToken).GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>()).Role.ShouldBe("client");
    }

    [Fact]
    public async Task AcceptInvitation_WhenPending_MarksInvitationAcceptedWithClientId()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        Invitation invitation = await trainer.GetInvitationAsync(issued.Invitation.Id);
        invitation.Status.ShouldBe("accepted");
        invitation.ClientId.ShouldBe(auth.User.Id);
        invitation.AcceptedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task AcceptInvitation_WhenPending_PublishesClientRegistered()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithFirstName("Alex").WithLastName("Rivera").WithTimeZone("Asia/Tokyo").Build()))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        ClientRegistered registered = Factory.Events.OfType<ClientRegistered>().Where(e => e.UserId == auth.User.Id).ShouldHaveSingleItem();
        registered.TrainerId.ShouldBe(trainer.Id);
        registered.InvitationId.ShouldBe(issued.Invitation.Id);
        registered.Email.ShouldBe(issued.Invitation.Email);
        registered.FirstName.ShouldBe("Alex");
        registered.LastName.ShouldBe("Rivera");
        registered.TimeZone.ShouldBe("Asia/Tokyo");
        await Factory.WaitForMessagesAsync();
    }

    [Fact]
    public async Task AcceptInvitation_WithoutTimeZone_DefaultsToUtc()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithTimeZone(null).Build()))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        auth.User.TimeZone.ShouldBe("UTC");
    }

    [Fact]
    public async Task AcceptInvitation_UsesNamesFromTheRequestTrimmed()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithName("Alex", "Rivera"));

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithFirstName("  Alexandra ").WithLastName(" Rivera-Lopez ").Build()))
            .ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        auth.User.FirstName.ShouldBe("Alexandra");
        auth.User.LastName.ShouldBe("Rivera-Lopez");
    }

    [Fact]
    public async Task AcceptInvitation_ThenLogin_WorksWithTheChosenPassword()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithPassword("Chosen-pass-77").Build())).ShouldHaveStatusAsync(HttpStatusCode.Created);

        TestUser client = await LoginAsync(issued.Invitation.Email, "Chosen-pass-77");

        client.Role.ShouldBe("client");
        client.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task AcceptInvitation_Twice_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithPassword("Another-pass-1").Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        response.ShouldNotSetRefreshCookie();
        Factory.Events.OfType<ClientRegistered>().Count(e => e.InvitationId == issued.Invitation.Id).ShouldBe(1);
    }

    [Fact]
    public async Task AcceptInvitation_WhenCancelled_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        await ShouldHaveNoAccountAsync(issued.Invitation.Email);
    }

    [Fact]
    public async Task AcceptInvitation_WhenExpired_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.ExpireInvitationAsync(issued.Invitation.Id);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        await ShouldHaveNoAccountAsync(issued.Invitation.Email);
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("expired");
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task AcceptInvitation_WithUnknownToken_Returns404(string token)
    {
        HttpResponseMessage response = await Factory.AcceptInvitationAsync(token);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task AcceptInvitation_WithInvitationId_Returns404()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Invitation.Id.ToString());

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task AcceptInvitation_WhenEmailWasRegisteredMeanwhile_Returns409EmailInUseAndStaysPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.SignupAsync(s => s.WithEmail(issued.Invitation.Email));

        HttpResponseMessage response = await Factory.AcceptInvitationAsync(issued.Token);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("pending");
        Factory.Events.OfType<ClientRegistered>().ShouldNotContain(e => e.InvitationId == issued.Invitation.Id);
    }

    [Fact]
    public async Task AcceptInvitation_ConcurrentlyWithTheSameToken_CreatesExactlyOneClient()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => Factory.AcceptInvitationAsync(issued.Token)));

        responses.Count(r => r.StatusCode == HttpStatusCode.Created).ShouldBe(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.Created).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Conflict);
        (await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM identity.users WHERE email = @Email", new { issued.Invitation.Email })).ShouldBe(1);
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task AcceptInvitation_WithInvalidFirstName_Returns400FirstNameError(string firstName)
    {
        IssuedInvitation issued = await InviteAsync(await CreateTrainerAsync());

        await ShouldOnlyFailOn(await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithFirstName(firstName).Build()), "firstName");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task AcceptInvitation_WithInvalidLastName_Returns400LastNameError(string lastName)
    {
        IssuedInvitation issued = await InviteAsync(await CreateTrainerAsync());

        await ShouldOnlyFailOn(await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithLastName(lastName).Build()), "lastName");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(129)]
    public async Task AcceptInvitation_WithPasswordOfInvalidLength_Returns400PasswordError(int length)
    {
        IssuedInvitation issued = await InviteAsync(await CreateTrainerAsync());

        await ShouldOnlyFailOn(await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithPassword(new string('p', length)).Build()), "password");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not/AZone")]
    public async Task AcceptInvitation_WithInvalidTimeZone_Returns400TimeZoneError(string timeZone)
    {
        IssuedInvitation issued = await InviteAsync(await CreateTrainerAsync());

        await ShouldOnlyFailOn(await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithTimeZone(timeZone).Build()), "timeZone");
    }

    [Fact]
    public async Task AcceptInvitation_WithInvalidRequest_LeavesInvitationPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await Factory.AcceptInvitationAsync(issued.Token, new AcceptInvitationBuilder().WithPassword("short").Build());

        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("pending");
        await ShouldHaveNoAccountAsync(issued.Invitation.Email);
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    private static async Task ShouldOnlyFailOn(HttpResponseMessage response, string field)
    {
        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
        response.ShouldNotSetRefreshCookie();
    }

    private async Task ShouldHaveNoAccountAsync(string email) =>
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(email, new AcceptInvitationBuilder().Build().Password)))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
}
