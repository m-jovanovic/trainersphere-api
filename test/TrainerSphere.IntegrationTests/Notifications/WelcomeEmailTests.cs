using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The trainer welcome email (section 15): sent once to a trainer who signs up, never to clients.
/// </summary>
public sealed class WelcomeEmailTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task Signup_SendsExactlyOneEmailToTheTrainer()
    {
        TestUser trainer = await CreateTrainerAsync(b => b.WithName("Maya", "Novak"));
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.To(trainer.Email).ShouldHaveSingleItem();
        email.Kind.ShouldBe(EmailKinds.TrainerWelcome);
        email.To.ShouldBe(trainer.Email);
        email.ToName.ShouldBe("Maya Novak");
        email.TextBody.ShouldStartWith("Welcome to TrainerSphere, Maya");
    }

    [Fact]
    public async Task WelcomeEmail_LinksToLogin()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.TrainerWelcome);

        email.TextBody.ShouldContain("http://web.test/login");
        email.HtmlBody.ShouldContain("href=\"http://web.test/login\"");
    }

    [Fact]
    public async Task AcceptInvitation_SendsNoWelcomeToTheClient()
    {
        TestUser trainer = await CreateTrainerAsync();

        TestUser client = await CreateClientAsync(trainer);

        Factory.Emails.To(client.Email).ShouldHaveSingleItem().Kind.ShouldBe(EmailKinds.Invitation);
    }

    [Fact]
    public async Task Login_SendsNoEmail()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();

        await LoginAsync(trainer.Email, trainer.Password);
        await Factory.WaitForMessagesAsync();

        Factory.Emails.To(trainer.Email).ShouldHaveSingleItem().Kind.ShouldBe(EmailKinds.TrainerWelcome);
    }
}
