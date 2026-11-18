using System.Net;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The client invitation email (section 15): sent to the invitee with the link <c>{webBaseUrl}/invite/{token}</c>.
/// </summary>
public sealed class InvitationEmailTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CreateInvitation_SendsOneEmailToTheInviteeOnly()
    {
        TestUser trainer = await CreateTrainerAsync();

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithName("Alex", "Rivera"));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.To(issued.Invitation.Email).ShouldHaveSingleItem();
        email.Kind.ShouldBe(EmailKinds.Invitation);
        email.To.ShouldBe(issued.Invitation.Email);
        email.ToName.ShouldBe("Alex");
        Factory.Emails.To(trainer.Email).ShouldNotContain(m => m.Kind == EmailKinds.Invitation);
    }

    [Fact]
    public async Task InvitationEmail_Link_OpensThePendingInvitation()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();

        string token = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation).InvitationToken();

        InvitationPreview preview = await (await Factory.PreviewInvitationAsync(token)).ShouldHaveJsonAsync<InvitationPreview>();
        preview.Email.ShouldBe(issued.Invitation.Email);
        preview.TrainerName.ShouldBe($"{trainer.FirstName} {trainer.LastName}");
        preview.Status.ShouldBe("pending");
    }

    [Fact]
    public async Task InvitationEmail_Link_AcceptsTheInvitation()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();
        string token = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation).InvitationToken();

        AuthResponse auth = await (await Factory.AcceptInvitationAsync(token)).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        auth.User.Email.ShouldBe(issued.Invitation.Email);
        auth.User.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task InvitationEmail_HtmlButtonAndTextCarryTheSameLink()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);

        email.HtmlBody.ShouldContain($"href=\"http://web.test/invite/{email.InvitationToken()}\"");
    }

    [Fact]
    public async Task InvitationEmail_StatesTheExpiryTheApiReports()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);

        email.TextBody.ShouldContain(NotificationFlows.EmailDate(issued.Invitation.ExpiresAt));
    }

    [Fact]
    public async Task InvitationEmail_WithoutMessage_HasNoQuote()
    {
        TestUser trainer = await CreateTrainerAsync();

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage(null));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);
        email.TextBody.ShouldNotContain("\n> ");
        email.HtmlBody.ShouldNotContain("<blockquote");
    }

    [Fact]
    public async Task InvitationEmail_MultiLineMessage_KeepsEveryLine()
    {
        TestUser trainer = await CreateTrainerAsync();

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage("Line one\nLine two"));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(issued.Invitation.Email, EmailKinds.Invitation);
        email.TextBody.ShouldContain("> Line one");
        email.TextBody.ShouldContain("> Line two");
        email.HtmlBody.ShouldContain("Line one<br>Line two");
    }

    [Fact]
    public async Task CancelInvitation_SendsNoEmail()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await Factory.WaitForMessagesAsync();

        Factory.Emails.To(issued.Invitation.Email).ShouldHaveSingleItem().Kind.ShouldBe(EmailKinds.Invitation);
    }

    [Fact]
    public async Task ResendInvitation_SendsTheNewEmailToTheSameInvitee()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage("Original note"));

        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await Factory.WaitForMessagesAsync();

        EmailMessage[] emails = [.. Factory.Emails.To(issued.Invitation.Email)];
        emails.Length.ShouldBe(2);
        emails.ShouldAllBe(m => m.Kind == EmailKinds.Invitation && m.To == issued.Invitation.Email);
        emails[1].TextBody.ShouldContain("Original note");
    }
}
