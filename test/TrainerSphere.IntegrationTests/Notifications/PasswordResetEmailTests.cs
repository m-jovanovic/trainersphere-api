using System.Net;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The password reset email (sections 4 and 15): link <c>{webBaseUrl}/reset-password?token={token}</c>, valid 1 h.
/// </summary>
public sealed class PasswordResetEmailTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ForgotPassword_ForClient_SendsAWorkingResetLinkToTheClient()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();
        TestUser client = clients[0];

        await Factory.RequestPasswordResetAsync(client.Email);

        string token = Factory.Emails.Single(client.Email, EmailKinds.PasswordReset).ResetToken();
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ResetPassword, new ResetPasswordRequest(token, "Client-new-pass-1")))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await LoginAsync(client.Email, "Client-new-pass-1");
    }

    [Fact]
    public async Task ForgotPassword_WithDifferentCasing_SendsToTheStoredAddress()
    {
        TestUser trainer = await CreateTrainerAsync();

        await Factory.RequestPasswordResetAsync("  " + trainer.Email.ToUpperInvariant() + " ");

        Factory.Emails.All.ShouldContain(m => m.Kind == EmailKinds.PasswordReset && string.Equals(m.To, trainer.Email, StringComparison.Ordinal));
    }

    [Fact]
    public async Task PasswordResetEmail_GreetsByTheCurrentName()
    {
        TestUser trainer = await CreateTrainerAsync();
        await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Renamed", "Coach", "kg", "UTC"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        await Factory.RequestPasswordResetAsync(trainer.Email);

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.PasswordReset);
        email.ToName.ShouldBe("Renamed");
        email.TextBody.ShouldContain("Hi Renamed,");
    }

    [Fact]
    public async Task PasswordResetEmail_HtmlButtonAndTextCarryTheSameLink()
    {
        TestUser trainer = await CreateTrainerAsync();

        await Factory.RequestPasswordResetAsync(trainer.Email);

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.PasswordReset);
        string token = email.ResetToken();
        email.HtmlBody.ShouldContain($"href=\"http://web.test/reset-password?token={Uri.EscapeDataString(token)}\"");
    }

    [Fact]
    public async Task PasswordResetEmail_StatesAnExpiryOneHourAway()
    {
        TestUser trainer = await CreateTrainerAsync();
        DateTimeOffset requestedAt = DateTimeOffset.UtcNow;

        await Factory.RequestPasswordResetAsync(trainer.Email);

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.PasswordReset);
        string[] candidates = [NotificationFlows.EmailDate(requestedAt.AddHours(1)), NotificationFlows.EmailDate(requestedAt.AddHours(1).AddMinutes(1))];
        candidates.ShouldContain(c => email.TextBody.Contains(c, StringComparison.Ordinal), email.TextBody);
    }

    [Fact]
    public async Task ForgotPasswordTwice_SendsTwoEmailsWithDifferentLinks()
    {
        TestUser trainer = await CreateTrainerAsync();

        await Factory.RequestPasswordResetAsync(trainer.Email);
        await Factory.RequestPasswordResetAsync(trainer.Email);

        EmailMessage[] emails = [.. Factory.Emails.To(trainer.Email).Where(m => m.Kind == EmailKinds.PasswordReset)];
        emails.Length.ShouldBe(2);
        emails[0].ResetToken().ShouldNotBe(emails[1].ResetToken());
    }
}
