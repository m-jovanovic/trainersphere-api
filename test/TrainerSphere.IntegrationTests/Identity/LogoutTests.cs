using System.Net;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class LogoutTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Logout_WithCookie_Returns204AndClearsCookie()
    {
        SignedIn session = await Factory.SignupAsync();

        HttpResponseMessage response = await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken, session.AccessToken);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Logout_WithCookie_RevokesThatRefreshToken()
    {
        SignedIn session = await Factory.SignupAsync();

        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Logout_FromBrowserClient_EndsTheSession()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await trainer.Client.PostAsync(IdentityRoutes.Logout, content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await trainer.Client.PostAsync(IdentityRoutes.Refresh, content: null, Ct)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task Logout_WithoutBearer_StillRevokesTheCookie()
    {
        SignedIn session = await Factory.SignupAsync();

        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken, accessToken: null)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithExpiredBearer_Returns204AndRevokes()
    {
        SignedIn session = await Factory.SignupAsync();
        string expired = AccessTokens.Create(session.User.Id, "trainer", expires: DateTime.UtcNow.AddHours(-1));

        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken, expired)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_OnlyEndsTheCurrentSession()
    {
        SignedIn session = await Factory.SignupAsync();
        SignedIn otherDevice = await Factory.LoginAsync(session.User.Email, session.Password);

        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await Factory.RotateAsync(otherDevice.RefreshToken);
    }

    [Fact]
    public async Task Logout_WithoutCookie_Returns204AndClearsCookie()
    {
        HttpResponseMessage response = await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, token: null);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Logout_WithUnknownCookie_Returns204AndClearsCookie()
    {
        HttpResponseMessage response = await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, "not-a-real-token");

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        response.ShouldClearRefreshCookie();
    }

    [Fact]
    public async Task Logout_Twice_Returns204BothTimes()
    {
        SignedIn session = await Factory.SignupAsync();

        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await (await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task Logout_ThenLogin_StartsAWorkingSession()
    {
        SignedIn session = await Factory.SignupAsync();
        await Factory.PostWithRefreshCookieAsync(IdentityRoutes.Logout, session.RefreshToken);

        SignedIn again = await Factory.LoginAsync(session.User.Email, session.Password);

        await Factory.RotateAsync(again.RefreshToken);
    }
}
