using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// <c>notifications.email_log</c>: one row per sent email, tied to the integration event that caused it.
/// </summary>
public sealed class EmailLogTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task EveryEmail_IsLoggedWithTheEventThatCausedIt()
    {
        DateTime start = DateTime.UtcNow.AddSeconds(-1);
        TestUser trainer = await CreateTrainerAsync();
        TestUser client = await CreateClientAsync(trainer);
        await Factory.RequestPasswordResetAsync(client.Email);

        (EmailMessage Email, Guid EventId)[] expected =
        [
            (Factory.Emails.Single(trainer.Email, EmailKinds.TrainerWelcome), Factory.Events.Last<TrainerRegistered>(e => e.UserId == trainer.Id).Id),
            (Factory.Emails.Single(client.Email, EmailKinds.Invitation), Factory.Events.Last<InvitationIssued>(e => e.Email == client.Email).Id),
            (Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined), Factory.Events.Last<ClientRegistered>(e => e.UserId == client.Id).Id),
            (Factory.Emails.Single(client.Email, EmailKinds.PasswordReset), Factory.Events.Last<PasswordResetRequested>(e => e.UserId == client.Id).Id),
        ];

        EmailLogRow[] log = [.. await Factory.EmailLogAsync(trainer.Email), .. await Factory.EmailLogAsync(client.Email)];
        log.Length.ShouldBe(expected.Length);

        foreach ((EmailMessage email, Guid eventId) in expected)
        {
            EmailLogRow row = log.Where(r => r.Kind == email.Kind && r.Recipient == email.To).ShouldHaveSingleItem();
            row.Subject.ShouldBe(email.Subject);
            row.SourceEventId.ShouldBe(eventId);
            row.SentAt.ShouldBeInRange(start, DateTime.UtcNow.AddSeconds(1));
        }
    }

    [Fact]
    public async Task UnknownEmailReset_IsNotLogged()
    {
        string email = Builders.Unique.Email("nobody");

        await Factory.RequestPasswordResetAsync(email);

        (await Factory.EmailLogAsync(email)).ShouldBeEmpty();
    }
}
