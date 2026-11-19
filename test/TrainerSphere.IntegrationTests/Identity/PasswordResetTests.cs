using System.Net;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class PasswordResetTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string NewPassword = "Reset-pass-2026";

    [Fact]
    public async Task ForgotPassword_ForKnownEmail_Returns202WithoutBody()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await ForgotAsync(trainer.Email);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ForgotPassword_ForKnownEmail_PublishesOneHourResetToken()
    {
        TestUser trainer = await CreateTrainerAsync();

        await ForgotAsync(trainer.Email);

        PasswordResetRequested requested = Factory.Events.OfType<PasswordResetRequested>().Where(e => e.UserId == trainer.Id).ShouldHaveSingleItem();
        requested.Email.ShouldBe(trainer.Email);
        requested.FirstName.ShouldBe(trainer.FirstName);
        requested.Token.Length.ShouldBe(43);
        requested.Token.ShouldMatch("^[A-Za-z0-9_-]+$");
        (requested.ExpiresAt - requested.OccurredAt).ShouldBe(TimeSpan.FromHours(1));
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_Returns202AndPublishesNothing()
    {
        string email = Unique.Email("nobody");

        HttpResponseMessage response = await ForgotAsync(email);

        await response.ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        Factory.Events.OfType<PasswordResetRequested>().ShouldNotContain(e => e.Email == email);
    }

    [Fact]
    public async Task ForgotPassword_WithPaddedUpperCaseEmail_PublishesForTheAccount()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await ForgotAsync($" {trainer.Email.ToUpperInvariant()} ")).ShouldHaveStatusAsync(HttpStatusCode.Accepted);

        Factory.Events.OfType<PasswordResetRequested>().ShouldContain(e => e.UserId == trainer.Id);
    }

    [Fact]
    public async Task ForgotPassword_ForClient_PublishesForTheClient()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        await (await ForgotAsync(clients[0].Email)).ShouldHaveStatusAsync(HttpStatusCode.Accepted);

        Factory.Events.OfType<PasswordResetRequested>().ShouldContain(e => e.UserId == clients[0].Id);
    }

    [Fact]
    public async Task ForgotPassword_DoesNotChangeThePasswordOrSessions()
    {
        SignedIn session = await Factory.SignupAsync();

        await ForgotAsync(session.User.Email);

        await Factory.LoginAsync(session.User.Email, session.Password);
        await Factory.RotateAsync(session.RefreshToken);
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public async Task ForgotPassword_WithInvalidEmail_Returns400EmailError(string email)
    {
        HttpResponseMessage response = await ForgotAsync(email);

        await response.ShouldBeValidationProblemAsync("email");
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_Returns204()
    {
        TestUser trainer = await CreateTrainerAsync();
        string token = await RequestResetTokenAsync(trainer);

        HttpResponseMessage response = await ResetAsync(token, NewPassword);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_ReplacesThePassword()
    {
        TestUser trainer = await CreateTrainerAsync();
        string token = await RequestResetTokenAsync(trainer);

        await (await ResetAsync(token, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await LoginAsync(trainer.Email, NewPassword);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(trainer.Email, trainer.Password)))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    [Fact]
    public async Task ResetPassword_WithValidToken_RevokesEveryRefreshToken()
    {
        SignedIn session = await Factory.SignupAsync();
        SignedIn otherDevice = await Factory.LoginAsync(session.User.Email, session.Password);
        await ForgotAsync(session.User.Email);
        string token = Factory.Events.Last<PasswordResetRequested>(e => e.UserId == session.User.Id).Token;

        await (await ResetAsync(token, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
        await (await Factory.RefreshWithCookieAsync(otherDevice.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task ResetPassword_ForClient_ReplacesThePassword()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();
        string token = await RequestResetTokenAsync(clients[0]);

        await (await ResetAsync(token, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await LoginAsync(clients[0].Email, NewPassword)).Role.ShouldBe("client");
    }

    [Fact]
    public async Task ResetPassword_WithUsedToken_Returns400TokenError()
    {
        TestUser trainer = await CreateTrainerAsync();
        string token = await RequestResetTokenAsync(trainer);
        await (await ResetAsync(token, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await ResetAsync(token, "Another-pass-2026");

        await ShouldBeInvalidTokenAsync(response);
        await LoginAsync(trainer.Email, NewPassword);
    }

    [Fact]
    public async Task ResetPassword_WithExpiredToken_Returns400TokenErrorAndKeepsPassword()
    {
        TestUser trainer = await CreateTrainerAsync();
        string token = await RequestResetTokenAsync(trainer);
        await Factory.ExecuteAsync(
            "UPDATE identity.password_reset_tokens SET expires_at = now() - interval '1 minute' WHERE user_id = @Id",
            new { trainer.Id });

        HttpResponseMessage response = await ResetAsync(token, NewPassword);

        await ShouldBeInvalidTokenAsync(response);
        await LoginAsync(trainer.Email, trainer.Password);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task ResetPassword_WithUnknownToken_Returns400TokenError(string token)
    {
        HttpResponseMessage response = await ResetAsync(token, NewPassword);

        await ShouldBeInvalidTokenAsync(response);
    }

    [Fact]
    public async Task ResetPassword_UsingTheLatestLink_InvalidatesEarlierLinks()
    {
        TestUser trainer = await CreateTrainerAsync();
        string first = await RequestResetTokenAsync(trainer);
        string second = await RequestResetTokenAsync(trainer);
        first.ShouldNotBe(second);

        await (await ResetAsync(second, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await ShouldBeInvalidTokenAsync(await ResetAsync(first, "Another-pass-2026"));
    }

    [Fact]
    public async Task ResetPassword_UsingAnEarlierLink_StillWorksWhileUnused()
    {
        TestUser trainer = await CreateTrainerAsync();
        string first = await RequestResetTokenAsync(trainer);
        await RequestResetTokenAsync(trainer);

        await (await ResetAsync(first, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await LoginAsync(trainer.Email, NewPassword);
    }

    [Fact]
    public async Task ResetPassword_WithEmptyToken_Returns400TokenError()
    {
        HttpResponseMessage response = await ResetAsync(string.Empty, NewPassword);

        await response.ShouldBeValidationProblemAsync("token");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(129)]
    public async Task ResetPassword_WithNewPasswordOfInvalidLength_Returns400AndKeepsTokenUsable(int length)
    {
        TestUser trainer = await CreateTrainerAsync();
        string token = await RequestResetTokenAsync(trainer);

        Problem problem = await (await ResetAsync(token, new string('p', length))).ShouldBeValidationProblemAsync("newPassword");

        problem.Errors.ShouldNotBeNull().Keys.ShouldBe(["newPassword"]);
        await (await ResetAsync(token, NewPassword)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task ResetPassword_WithInvalidToken_DoesNotRevokeSessions()
    {
        SignedIn session = await Factory.SignupAsync();

        await ResetAsync("not-a-real-token", NewPassword);

        await Factory.RotateAsync(session.RefreshToken);
    }

    private static async Task ShouldBeInvalidTokenAsync(HttpResponseMessage response)
    {
        Problem problem = await response.ShouldBeValidationProblemAsync("token");
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe(["token"]);
    }

    private async Task<string> RequestResetTokenAsync(TestUser user)
    {
        await (await ForgotAsync(user.Email)).ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        return Factory.Events.Last<PasswordResetRequested>(e => e.UserId == user.Id).Token;
    }

    private Task<HttpResponseMessage> ForgotAsync(string email) =>
        CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ForgotPassword, new ForgotPasswordRequest(email));

    private Task<HttpResponseMessage> ResetAsync(string token, string newPassword) =>
        CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ResetPassword, new ResetPasswordRequest(token, newPassword));
}
