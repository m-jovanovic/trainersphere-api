using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

public sealed partial class EmailTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Signup_SendsWelcomeEmailToTrainer()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.TrainerWelcome);

        email.ToName.ShouldBe($"{trainer.FirstName} {trainer.LastName}");
        email.Subject.ShouldBe("Welcome to TrainerSphere");
        email.TextBody.ShouldContain("http://web.test/login");
    }

    [Fact]
    public async Task CreateInvitation_SendsInvitationEmailWithLinkAndMessage()
    {
        TestUser trainer = await CreateTrainerAsync();

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage("See you Monday at 7!"));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);
        email.Subject.ShouldBe($"{trainer.FirstName} {trainer.LastName} invited you to TrainerSphere");
        email.TextBody.ShouldContain($"http://web.test/invite/{issued.Token}");
        email.HtmlBody.ShouldContain($"href=\"http://web.test/invite/{issued.Token}\"");
        email.TextBody.ShouldContain("See you Monday at 7!");
    }

    [Fact]
    public async Task CreateInvitation_HtmlEncodesUserText()
    {
        TestUser trainer = await CreateTrainerAsync();

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage("<script>alert(1)</script>"));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);
        email.HtmlBody.ShouldNotContain("<script>");
        email.HtmlBody.ShouldContain("&lt;script&gt;alert(1)&lt;/script&gt;");
    }

    [Fact]
    public async Task ResendInvitation_SendsNewLinkThatReplacesTheOldOne()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await (await trainer.Client.PostAsync($"/api/invitations/{issued.Invitation.Id}/resend", content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await Factory.WaitForMessagesAsync();

        EmailMessage[] emails = [.. Factory.Emails.To(issued.Invitation.Email).Where(m => m.Kind == EmailKinds.Invitation)];
        emails.Length.ShouldBe(2);
        string newToken = InvitationLink().Match(emails[1].TextBody).Groups["token"].Value;
        newToken.ShouldNotBe(issued.Token);

        InvitationPreview preview = await (await CreateAnonymousClient().GetAsync($"/api/invitations/{newToken}", Ct)).ShouldHaveJsonAsync<InvitationPreview>();
        preview.Status.ShouldBe("pending");
        await (await CreateAnonymousClient().GetAsync($"/api/invitations/{issued.Token}", Ct)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task AcceptInvitation_SendsClientJoinedEmailToTrainer()
    {
        TestUser trainer = await CreateTrainerAsync();

        TestUser client = await CreateClientAsync(trainer);

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined);
        email.Subject.ShouldBe($"{client.FirstName} {client.LastName} accepted your invitation");
        email.TextBody.ShouldContain(client.Email);
        email.TextBody.ShouldContain($"http://web.test/clients/{client.Id}");
    }

    [Fact]
    public async Task ClientJoinedEmail_UsesTrainerNameFromLatestProfileUpdate()
    {
        TestUser trainer = await CreateTrainerAsync();
        await (await trainer.Client.PutAsJsonAsync("/api/auth/me", new UpdateMeRequest("Coach", "Renamed", "kg", "Europe/Belgrade"), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.OK);

        await CreateClientAsync(trainer);

        Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined).ToName.ShouldBe("Coach Renamed");
    }

    [Fact]
    public async Task ForgotPassword_SendsResetLinkThatResetsThePassword()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await CreateAnonymousClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(trainer.Email), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.PasswordReset);
        Match link = ResetLink().Match(email.TextBody);
        link.Success.ShouldBeTrue(email.TextBody);
        string token = Uri.UnescapeDataString(link.Groups["token"].Value);
        token.ShouldBe(Factory.Events.Last<PasswordResetRequested>(e => e.UserId == trainer.Id).Token);

        await (await CreateAnonymousClient().PostAsJsonAsync("/api/auth/reset-password", new ResetPasswordRequest(token, "Brand-new-pass-1"), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await LoginAsync(trainer.Email, "Brand-new-pass-1");
    }

    [Fact]
    public async Task ForgotPassword_ForUnknownEmail_SendsNothing()
    {
        string email = Builders.Unique.Email("nobody");

        await (await CreateAnonymousClient().PostAsJsonAsync("/api/auth/forgot-password", new ForgotPasswordRequest(email), HttpAssertions.Json, Ct))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        await Factory.WaitForMessagesAsync();

        Factory.Emails.To(email).ShouldBeEmpty();
    }

    [Fact]
    public async Task SentEmails_AreRecordedInTheEmailLog()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();

        IReadOnlyList<string> kinds = await Factory.QueryAsync<string>(
            "SELECT kind FROM notifications.email_log WHERE recipient = @Email",
            new { trainer.Email });

        kinds.ShouldBe([EmailKinds.TrainerWelcome]);
    }

    [GeneratedRegex(@"http://web\.test/invite/(?<token>[A-Za-z0-9_\-]+)")]
    private static partial Regex InvitationLink();

    [GeneratedRegex(@"http://web\.test/reset-password\?token=(?<token>[A-Za-z0-9_\-%]+)")]
    private static partial Regex ResetLink();
}
