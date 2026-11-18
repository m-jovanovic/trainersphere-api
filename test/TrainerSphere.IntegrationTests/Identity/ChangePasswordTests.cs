using System.Net;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class ChangePasswordTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string NewPassword = "Brand-new-pass-1";

    [Fact]
    public async Task ChangePassword_WithCorrectCurrentPassword_Returns204()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(trainer.Password, NewPassword));

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await response.Content.ReadAsStringAsync(Ct)).ShouldBeEmpty();
    }

    [Fact]
    public async Task ChangePassword_ThenLogin_AcceptsOnlyTheNewPassword()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(trainer.Password, NewPassword))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await LoginAsync(trainer.Email, NewPassword);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(trainer.Email, trainer.Password)))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    [Fact]
    public async Task ChangePassword_RevokesEveryRefreshTokenOfTheUser()
    {
        SignedIn session = await Factory.SignupAsync();
        SignedIn otherDevice = await Factory.LoginAsync(session.User.Email, session.Password);

        await (await Factory.CreateBearerClient(session.AccessToken).PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(session.Password, NewPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await (await Factory.RefreshWithCookieAsync(session.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
        await (await Factory.RefreshWithCookieAsync(otherDevice.RefreshToken)).ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidRefreshToken");
    }

    [Fact]
    public async Task ChangePassword_DoesNotAffectOtherUsersSessions()
    {
        SignedIn session = await Factory.SignupAsync();
        SignedIn other = await Factory.SignupAsync();

        await Factory.CreateBearerClient(session.AccessToken).PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(session.Password, NewPassword));

        await Factory.RotateAsync(other.RefreshToken);
    }

    [Fact]
    public async Task ChangePassword_AsClient_Returns204()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        await (await clients[0].Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(clients[0].Password, NewPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await LoginAsync(clients[0].Email, NewPassword);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_Returns400CurrentPasswordError()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest("Wrong-pass-123", NewPassword));

        Problem problem = await response.ShouldBeValidationProblemAsync("currentPassword");
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe(["currentPassword"]);
    }

    [Fact]
    public async Task ChangePassword_WithWrongCurrentPassword_KeepsPasswordAndSessions()
    {
        SignedIn session = await Factory.SignupAsync();

        await Factory.CreateBearerClient(session.AccessToken).PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest("Wrong-pass-123", NewPassword));

        await Factory.RotateAsync(session.RefreshToken);
        await Factory.LoginAsync(session.User.Email, session.Password);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(session.User.Email, NewPassword)))
            .ShouldHaveStatusAsync(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ChangePassword_WithEmptyCurrentPassword_Returns400CurrentPasswordError()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(string.Empty, NewPassword));

        await response.ShouldBeValidationProblemAsync("currentPassword");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(7)]
    [InlineData(129)]
    public async Task ChangePassword_WithNewPasswordOfInvalidLength_Returns400NewPasswordError(int length)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(trainer.Password, new string('p', length)));

        Problem problem = await response.ShouldBeValidationProblemAsync("newPassword");
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe(["newPassword"]);
        await LoginAsync(trainer.Email, trainer.Password);
    }

    [Theory]
    [InlineData(8)]
    [InlineData(128)]
    public async Task ChangePassword_WithNewPasswordAtLengthLimit_Returns204(int length)
    {
        TestUser trainer = await CreateTrainerAsync();
        string password = new('p', length);

        await (await trainer.Client.PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest(trainer.Password, password))).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        await LoginAsync(trainer.Email, password);
    }

    [Fact]
    public async Task ChangePassword_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ChangePassword, new ChangePasswordRequest("Trainer-pass-1", NewPassword));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }
}
