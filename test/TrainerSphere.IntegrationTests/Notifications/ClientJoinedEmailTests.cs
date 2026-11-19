using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The "client joined" email (section 15): sent to the inviting trainer, linking to <c>/clients/{id}</c>.
/// </summary>
public sealed class ClientJoinedEmailTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task AcceptInvitation_NotifiesOnlyTheInvitingTrainer()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser otherTrainer = await CreateTrainerAsync();

        TestUser client = await CreateClientAsync(trainer);

        Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined).To.ShouldBe(trainer.Email);
        Factory.Emails.To(otherTrainer.Email).ShouldNotContain(m => m.Kind == EmailKinds.ClientJoined);
        Factory.Emails.To(client.Email).ShouldNotContain(m => m.Kind == EmailKinds.ClientJoined);
    }

    [Fact]
    public async Task ClientJoinedEmail_LinksToTheClientDetailPage()
    {
        TestUser trainer = await CreateTrainerAsync();

        TestUser client = await CreateClientAsync(trainer);

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined);
        email.TextBody.ShouldContain($"http://web.test/clients/{client.Id}");
        email.HtmlBody.ShouldContain($"href=\"http://web.test/clients/{client.Id}\"");
    }

    [Fact]
    public async Task ClientJoinedEmail_UsesTheNamesTheClientAcceptedWith()
    {
        TestUser trainer = await CreateTrainerAsync();

        TestUser client = await CreateClientAsync(trainer, a => a.WithFirstName("Alexandra").WithLastName("Rivera-Stone"));

        EmailMessage email = Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined);
        email.Subject.ShouldBe("Alexandra Rivera-Stone accepted your invitation");
        email.TextBody.ShouldContain($"Alexandra Rivera-Stone ({client.Email})");
    }

    [Fact]
    public async Task TwoClientsJoining_SendOneEmailEach()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync(2);

        EmailMessage[] emails = [.. Factory.Emails.To(trainer.Email).Where(m => m.Kind == EmailKinds.ClientJoined)];

        emails.Length.ShouldBe(2);
        foreach (TestUser client in clients)
        {
            emails.ShouldContain(m => m.TextBody.Contains($"/clients/{client.Id}", StringComparison.Ordinal));
        }
    }
}
