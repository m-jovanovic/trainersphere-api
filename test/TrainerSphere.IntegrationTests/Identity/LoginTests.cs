using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class LoginTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Login_WithValidCredentials_Returns200AuthResponseForTheAccount()
    {
        SignedIn account = await Factory.SignupAsync();

        HttpResponseMessage response = await PostLoginAsync(account.User.Email, account.Password);

        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>();
        auth.TokenType.ShouldBe("Bearer");
        auth.ExpiresIn.ShouldBe(900);
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.ShouldBe(account.User with { CreatedAt = auth.User.CreatedAt });
        auth.User.CreatedAt.ShouldBe(account.User.CreatedAt, TimeSpan.FromMilliseconds(1));
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.AuthResponseProperties);
        body.GetProperty("user").ShouldHaveExactProperties(IdentityRoutes.UserProperties);
    }

    [Fact]
    public async Task Login_WithValidCredentials_SetsRefreshCookie()
    {
        SignedIn account = await Factory.SignupAsync();

        HttpResponseMessage response = await PostLoginAsync(account.User.Email, account.Password);

        (await response.ShouldHaveStatusAsync(HttpStatusCode.OK)).ShouldSetRefreshCookie().ShouldNotBe(account.RefreshToken);
    }

    [Fact]
    public async Task Login_WithValidCredentials_IssuesWorkingAccessToken()
    {
        SignedIn account = await Factory.SignupAsync();

        AuthResponse auth = await (await PostLoginAsync(account.User.Email, account.Password)).ShouldHaveJsonAsync<AuthResponse>();

        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Claim("sub").ShouldBe(account.User.Id.ToString());
        token.Claim("role").ShouldBe("trainer");
        (await (await Factory.CreateBearerClient(auth.AccessToken).GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>()).Id.ShouldBe(account.User.Id);
    }

    [Fact]
    public async Task Login_WithPaddedUpperCaseEmail_Succeeds()
    {
        SignedIn account = await Factory.SignupAsync();

        HttpResponseMessage response = await PostLoginAsync($"  {account.User.Email.ToUpperInvariant()} ", account.Password);

        (await response.ShouldHaveJsonAsync<AuthResponse>()).User.Id.ShouldBe(account.User.Id);
    }

    [Fact]
    public async Task Login_AsClient_ReturnsClientOfTheTrainer()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        AuthResponse auth = await (await PostLoginAsync(clients[0].Email, clients[0].Password)).ShouldHaveJsonAsync<AuthResponse>();

        auth.User.Id.ShouldBe(clients[0].Id);
        auth.User.Role.ShouldBe("client");
        auth.User.TrainerId.ShouldBe(trainer.Id);
        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Claim("role").ShouldBe("client");
        token.Claim("trainer_id").ShouldBe(trainer.Id.ToString());
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401InvalidCredentials()
    {
        SignedIn account = await Factory.SignupAsync();

        HttpResponseMessage response = await PostLoginAsync(account.User.Email, "Wrong-pass-123");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        response.ShouldNotSetRefreshCookie();
    }

    [Fact]
    public async Task Login_WithPasswordInOtherCase_Returns401InvalidCredentials()
    {
        SignedIn account = await Factory.SignupAsync();

        HttpResponseMessage response = await PostLoginAsync(account.User.Email, account.Password.ToUpperInvariant());

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Login_WithUnknownEmail_Returns401InvalidCredentials()
    {
        HttpResponseMessage response = await PostLoginAsync(Unique.Email("nobody"), "Whatever-pass-1");

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        response.ShouldNotSetRefreshCookie();
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_AreIndistinguishable()
    {
        SignedIn account = await Factory.SignupAsync();

        Problem wrongPassword = await (await PostLoginAsync(account.User.Email, "Wrong-pass-123")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
        Problem unknownEmail = await (await PostLoginAsync(Unique.Email("nobody"), "Wrong-pass-123")).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");

        unknownEmail.Title.ShouldBe(wrongPassword.Title);
        unknownEmail.Detail.ShouldBe(wrongPassword.Detail);
        unknownEmail.Detail.ShouldNotBeNull().ShouldNotContain("@");
    }

    [Fact]
    public async Task Login_Twice_StartsIndependentSessions()
    {
        SignedIn account = await Factory.SignupAsync();
        SignedIn first = await Factory.LoginAsync(account.User.Email, account.Password);
        SignedIn second = await Factory.LoginAsync(account.User.Email, account.Password);

        await Factory.RotateAsync(first.RefreshToken);
        await (await Factory.RefreshWithCookieAsync(first.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");

        await Factory.RotateAsync(second.RefreshToken);
    }

    [Theory]
    [InlineData("", "Trainer-pass-1", "email")]
    [InlineData("   ", "Trainer-pass-1", "email")]
    [InlineData("maya@example.com", "", "password")]
    public async Task Login_WithMissingCredential_Returns400ForThatField(string email, string password, string field)
    {
        HttpResponseMessage response = await PostLoginAsync(email, password);

        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
    }

    [Fact]
    public async Task Login_WithEmptyObject_Returns400ForEmailAndPassword()
    {
        HttpResponseMessage response = await CreateAnonymousClient().SendRawJsonAsync(HttpMethod.Post, IdentityRoutes.Login, "{}");

        await response.ShouldBeValidationProblemAsync("email", "password");
    }

    private Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(email, password));
}
