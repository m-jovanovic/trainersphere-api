using Microsoft.Extensions.Options;
using Shouldly;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Features.ClientJoined;
using TrainerSphere.Api.Modules.Notifications.Features.InvitationEmail;
using TrainerSphere.Api.Modules.Notifications.Features.PasswordResetEmail;
using TrainerSphere.Api.Modules.Notifications.Features.TrainerWelcome;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;

namespace TrainerSphere.UnitTests.Notifications;

public sealed class EmailTemplateTests
{
    private static readonly DateTime Now = new(2026, 11, 18, 9, 0, 0, DateTimeKind.Utc);
    private static readonly WebLinks Links = new(Options.Create(new WebLinkOptions { WebBaseUrl = "https://app.example.com/" }));

    private static readonly Recipient Trainer = new(Guid.NewGuid(), "maya@example.com", "Maya", "Novak", Recipient.TrainerRole, TrainerId: null);

    [Fact]
    public void WebLinks_TrimTrailingSlashAndEscapeTokens()
    {
        Links.Invitation("a b").ShouldBe("https://app.example.com/invite/a%20b");
        Links.PasswordReset("x+y").ShouldBe("https://app.example.com/reset-password?token=x%2By");
        Links.Login().ShouldBe("https://app.example.com/login");
    }

    [Fact]
    public void InvitationEmail_ContainsLinkExpiryAndTrainerMessage()
    {
        InvitationIssued invitation = new(Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), "alex@example.com", "Alex", "Novak", "Maya Novak", "Excited to work with you!", "tok_123", Now, Now.AddDays(7));

        EmailMessage email = InvitationEmail.Compose(invitation, Links);

        email.Kind.ShouldBe(EmailKinds.Invitation);
        email.To.ShouldBe("alex@example.com");
        email.Subject.ShouldBe("Maya Novak invited you to TrainerSphere");
        email.TextBody.ShouldContain("https://app.example.com/invite/tok_123");
        email.TextBody.ShouldContain("> Excited to work with you!");
        email.TextBody.ShouldContain("25 Nov 2026 at 09:00 UTC");
        email.HtmlBody.ShouldContain("href=\"https://app.example.com/invite/tok_123\"");
    }

    [Fact]
    public void InvitationEmail_WithoutMessage_HasNoQuote()
    {
        InvitationIssued invitation = new(Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), "alex@example.com", "Alex", "Novak", "Maya Novak", Message: "  ", "tok", Now, Now.AddDays(7));

        EmailMessage email = InvitationEmail.Compose(invitation, Links);

        email.TextBody.ShouldNotContain("> ");
        email.HtmlBody.ShouldNotContain("<blockquote");
    }

    [Fact]
    public void Emails_HtmlEncodeEveryDynamicValue()
    {
        InvitationIssued invitation = new(Guid.NewGuid(), Now, Guid.NewGuid(), Guid.NewGuid(), "alex@example.com", "<b>Alex</b>", "Novak", "Maya & Co", "<img src=x onerror=alert(1)>", "tok", Now, Now.AddDays(7));

        EmailMessage email = InvitationEmail.Compose(invitation, Links);

        email.HtmlBody.ShouldNotContain("<img");
        email.HtmlBody.ShouldNotContain("<b>Alex");
        email.HtmlBody.ShouldContain("Maya &amp; Co");
        email.TextBody.ShouldContain("<b>Alex</b>");
    }

    [Fact]
    public void TrainerWelcome_AddressesTrainerAndLinksToLogin()
    {
        EmailMessage email = TrainerWelcomeEmail.Compose(Trainer, Links);

        email.Kind.ShouldBe(EmailKinds.TrainerWelcome);
        email.To.ShouldBe(Trainer.Email);
        email.ToName.ShouldBe("Maya Novak");
        email.TextBody.ShouldContain("https://app.example.com/login");
    }

    [Fact]
    public void ClientJoined_GoesToTrainerAndLinksToClient()
    {
        Recipient client = new(Guid.NewGuid(), "alex@example.com", "Alex", "Rivera", Recipient.ClientRole, Trainer.UserId);

        EmailMessage email = ClientJoinedEmail.Compose(Trainer, client, Links);

        email.Kind.ShouldBe(EmailKinds.ClientJoined);
        email.To.ShouldBe(Trainer.Email);
        email.Subject.ShouldBe("Alex Rivera accepted your invitation");
        email.TextBody.ShouldContain("alex@example.com");
        email.TextBody.ShouldContain($"https://app.example.com/clients/{client.UserId}");
    }

    [Fact]
    public void PasswordReset_ContainsEscapedLinkAndExpiry()
    {
        PasswordResetRequested request = new(Guid.NewGuid(), Now, Guid.NewGuid(), "maya@example.com", "Maya", "abc-_123", Now.AddHours(1));

        EmailMessage email = PasswordResetEmail.Compose(request, Links);

        email.Kind.ShouldBe(EmailKinds.PasswordReset);
        email.Subject.ShouldBe("Reset your TrainerSphere password");
        email.TextBody.ShouldContain("https://app.example.com/reset-password?token=abc-_123");
        email.TextBody.ShouldContain("18 Nov 2026 at 10:00 UTC");
    }
}
