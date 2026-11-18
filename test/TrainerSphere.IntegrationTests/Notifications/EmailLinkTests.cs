using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The web app served under a path, configured with a trailing slash.
/// </summary>
public sealed class SubPathWebApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    public const string WebBaseUrl = "http://web.test/coach";

    protected override void ConfigureSettings(IDictionary<string, string?> settings) => settings["App:WebBaseUrl"] = WebBaseUrl + "/";
}

/// <summary>
/// Links are <c>{webBaseUrl}/...</c> whatever form <c>App:WebBaseUrl</c> is configured in.
/// </summary>
public sealed class EmailLinkTests(SubPathWebApiFactory factory) : IntegrationTest(factory), IClassFixture<SubPathWebApiFactory>
{
    [Fact]
    public async Task Links_UnderABasePathWithTrailingSlash_KeepThePathWithoutDoubleSlashes()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser client = await CreateClientAsync(trainer);
        await Factory.RequestPasswordResetAsync(trainer.Email);

        EmailMessage invitation = Factory.Emails.Single(client.Email, EmailKinds.Invitation);
        invitation.InvitationToken(SubPathWebApiFactory.WebBaseUrl).ShouldNotBeNullOrEmpty();
        Factory.Emails.Single(trainer.Email, EmailKinds.TrainerWelcome).TextBody.ShouldContain($"{SubPathWebApiFactory.WebBaseUrl}/login");
        Factory.Emails.Single(trainer.Email, EmailKinds.ClientJoined).TextBody.ShouldContain($"{SubPathWebApiFactory.WebBaseUrl}/clients/{client.Id}");
        Factory.Emails.Single(trainer.Email, EmailKinds.PasswordReset).TextBody.ShouldContain($"{SubPathWebApiFactory.WebBaseUrl}/reset-password?token=");
        Factory.Emails.All.ShouldAllBe(m => !m.TextBody.Contains("coach//", StringComparison.Ordinal));
    }
}
