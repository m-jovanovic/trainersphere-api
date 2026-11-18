using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class RefreshTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string InvalidRefreshToken = "Auth.InvalidRefreshToken";

    [Fact]
    public async Task Refresh_WithValidCookie_Returns200AuthResponseForTheSameUser()
    {
        SignedIn session = await Factory.SignupAsync();

        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(session.RefreshToken);

        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>();
        auth.TokenType.ShouldBe("Bearer");
        auth.ExpiresIn.ShouldBe(900);
        auth.User.ShouldBe(session.User with { CreatedAt = auth.User.CreatedAt });
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.AuthResponseProperties);
        body.GetProperty("user").ShouldHaveExactProperties(IdentityRoutes.UserProperties);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_IssuesWorkingAccessToken()
    {
        SignedIn session = await Factory.SignupAsync();

        AuthResponse auth = await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldHaveJsonAsync<AuthResponse>();

        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Claim("sub").ShouldBe(session.User.Id.ToString());
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromMinutes(15));
        (await (await Factory.CreateBearerClient(auth.AccessToken).GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>()).Id.ShouldBe(session.User.Id);
    }

    [Fact]
    public async Task Refresh_WithValidCookie_RotatesTheCookie()
    {
        SignedIn session = await Factory.SignupAsync();

        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(session.RefreshToken);

        string rotated = (await response.ShouldHaveStatusAsync(HttpStatusCode.OK)).ShouldSetRefreshCookie();
        rotated.ShouldNotBe(session.RefreshToken);
    }

    [Fact]
    public async Task Refresh_RepeatedlyFromBrowserClient_KeepsWorking()
    {
        TestUser trainer = await CreateTrainerAsync();

        for (int i = 0; i < 4; i++)
        {
            AuthResponse auth = await (await trainer.Client.PostAsync(IdentityRoutes.Refresh, content: null, Ct)).ShouldHaveJsonAsync<AuthResponse>();
            auth.User.Id.ShouldBe(trainer.Id);
        }
    }

    [Fact]
    public async Task Refresh_WithRotatedCookie_Returns401AndClearsCookie()
    {
        SignedIn session = await Factory.SignupAsync();
        await Factory.RotateAsync(session.RefreshToken);

        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(session.RefreshToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Refresh_WithRotatedCookie_RevokesTheWholeFamily()
    {
        SignedIn session = await Factory.SignupAsync();
        string second = await Factory.RotateAsync(session.RefreshToken);
        string third = await Factory.RotateAsync(second);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);

        await (await Factory.RefreshWithCookieAsync(third)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
        await (await Factory.RefreshWithCookieAsync(second)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRotatedCookie_RevokesFamilyInTheDatabase()
    {
        SignedIn session = await Factory.SignupAsync();
        string current = await Factory.RotateAsync(session.RefreshToken);

        await Factory.RefreshWithCookieAsync(session.RefreshToken);

        (await Factory.QuerySingleAsync<int>(
            "SELECT count(*)::int FROM identity.refresh_tokens WHERE user_id = @Id AND revoked_at IS NULL",
            new { session.User.Id })).ShouldBe(0);
        current.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_ReuseInOneFamily_LeavesOtherSignInsOfTheUserWorking()
    {
        SignedIn session = await Factory.SignupAsync();
        SignedIn otherDevice = await Factory.LoginAsync(session.User.Email, session.Password);
        await Factory.RotateAsync(session.RefreshToken);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);

        await Factory.RotateAsync(otherDevice.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithoutCookie_Returns401AndClearsCookie()
    {
        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(token: null);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
        response.ShouldClearRefreshCookie();
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Refresh_WithUnknownCookie_Returns401AndClearsCookie(string token)
    {
        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(token);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Refresh_WithExpiredCookie_Returns401AndClearsCookie()
    {
        SignedIn session = await Factory.SignupAsync();
        await Factory.ExecuteAsync(
            "UPDATE identity.refresh_tokens SET expires_at = now() - interval '1 minute' WHERE user_id = @Id",
            new { session.User.Id });

        HttpResponseMessage response = await Factory.RefreshWithCookieAsync(session.RefreshToken);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, InvalidRefreshToken);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Refresh_WithExpiredAccessToken_StillRefreshes()
    {
        SignedIn session = await Factory.SignupAsync();
        string expired = AccessTokens.Create(session.User.Id, "trainer", expires: DateTime.UtcNow.AddMinutes(-30));

        HttpResponseMessage response = await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Refresh, session.RefreshToken, expired);

        (await response.ShouldHaveJsonAsync<AuthResponse>()).User.Id.ShouldBe(session.User.Id);
    }

    [Fact]
    public async Task Refresh_ConcurrentRequestsWithTheSameCookie_AtMostOneSucceeds()
    {
        SignedIn session = await Factory.SignupAsync();

        HttpResponseMessage[] responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Factory.RefreshWithCookieAsync(session.RefreshToken)));

        responses.Count(r => r.StatusCode == HttpStatusCode.OK).ShouldBeLessThanOrEqualTo(1);
        responses.Where(r => r.StatusCode != HttpStatusCode.OK).ShouldAllBe(r => r.StatusCode == HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_AfterProfileUpdate_ReturnsUpdatedUserAndClaims()
    {
        TestUser trainer = await CreateTrainerAsync();
        await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Renamed", "Coach", "lb", "Asia/Tokyo"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        AuthResponse auth = await (await trainer.Client.PostAsync(IdentityRoutes.Refresh, content: null, Ct)).ShouldHaveJsonAsync<AuthResponse>();

        auth.User.FirstName.ShouldBe("Renamed");
        auth.User.Units.ShouldBe("lb");
        auth.User.TimeZone.ShouldBe("Asia/Tokyo");
        AccessTokens.Read(auth.AccessToken).Claim("name").ShouldBe("Renamed Coach");
    }

    [Fact]
    public async Task Refresh_AsClient_KeepsClientRoleAndTrainer()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        AuthResponse auth = await (await clients[0].Client.PostAsync(IdentityRoutes.Refresh, content: null, Ct)).ShouldHaveJsonAsync<AuthResponse>();

        auth.User.Role.ShouldBe("client");
        auth.User.TrainerId.ShouldBe(trainer.Id);
        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Claim("role").ShouldBe("client");
        token.Claim("trainer_id").ShouldBe(trainer.Id.ToString());
    }

    [Fact]
    public async Task Refresh_RotatedToken_KeepsFamilyAndLinksReplacement()
    {
        SignedIn session = await Factory.SignupAsync();
        await Factory.RotateAsync(session.RefreshToken);

        IReadOnlyList<TokenRow> rows = await Factory.QueryAsync<TokenRow>(
            "SELECT id, family_id AS FamilyId, revoked_at IS NOT NULL AS Revoked, replaced_by_id AS ReplacedById FROM identity.refresh_tokens WHERE user_id = @Id ORDER BY created_at, id",
            new { session.User.Id });

        rows.Count.ShouldBe(2);
        rows.Select(r => r.FamilyId).Distinct().ShouldHaveSingleItem();
        rows[0].Revoked.ShouldBeTrue();
        rows[0].ReplacedById.ShouldBe(rows[1].Id);
        rows[1].Revoked.ShouldBeFalse();
    }

    private sealed record TokenRow(Guid Id, Guid FamilyId, bool Revoked, Guid? ReplacedById);
}
