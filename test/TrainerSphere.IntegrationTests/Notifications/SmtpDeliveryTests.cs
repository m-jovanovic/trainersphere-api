using System.Net;
using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// Real SMTP delivery: the API sends through <c>SmtpEmailSender</c> to a Mailpit container, read back over its HTTP API.
/// </summary>
public sealed class MailpitApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    private const int SmtpPort = 1025;
    private const int HttpPort = 8025;

    private readonly IContainer _mailpit = new ContainerBuilder("axllent/mailpit:v1.34.0")
        .WithPortBinding(SmtpPort, assignRandomHostPort: true)
        .WithPortBinding(HttpPort, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(HttpPort).ForPath("/livez")))
        .Build();

    public Uri MailpitApi => new($"http://{_mailpit.Hostname}:{_mailpit.GetMappedPublicPort(HttpPort)}");

    protected override bool CaptureEmails => false;

    public override async ValueTask InitializeAsync()
    {
        await _mailpit.StartAsync(TestContext.Current.CancellationToken);
        await base.InitializeAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _mailpit.DisposeAsync();
    }

    protected override void ConfigureSettings(IDictionary<string, string?> settings)
    {
        settings["Smtp:Host"] = _mailpit.Hostname;
        settings["Smtp:Port"] = _mailpit.GetMappedPublicPort(SmtpPort).ToString(System.Globalization.CultureInfo.InvariantCulture);
    }
}

public sealed class SmtpDeliveryTests(MailpitApiFactory factory) : IntegrationTest(factory), IClassFixture<MailpitApiFactory>
{
    private readonly MailpitApiFactory _mailpit = factory;

    [Fact]
    public async Task InvitationEmail_IsDeliveredOverSmtp()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();

        MailpitMessage message = await SingleDeliveredAsync(issued.Invitation.Email, $"{trainer.FirstName} {trainer.LastName} invited you to TrainerSphere");
        message.From.Address.ShouldBe("no-reply@trainersphere.dev");
        message.Text.ShouldContain($"http://web.test/invite/{issued.Token}");
        message.Html.ShouldContain($"href=\"http://web.test/invite/{issued.Token}\"");
    }

    [Fact]
    public async Task PasswordResetEmail_DeliveredOverSmtp_HasAWorkingLink()
    {
        TestUser trainer = await CreateTrainerAsync();

        await Factory.RequestPasswordResetAsync(trainer.Email);

        MailpitMessage message = await SingleDeliveredAsync(trainer.Email, "Reset your TrainerSphere password");
        string token = NotificationFlows.ResetToken(message.Text);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ResetPassword, new ResetPasswordRequest(token, "Smtp-new-pass-1")))
            .ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await LoginAsync(trainer.Email, "Smtp-new-pass-1");
    }

    [Fact]
    public async Task WelcomeEmail_DeliveredOverSmtp_ComesFromTrainerSphere()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();

        MailpitMessage message = await SingleDeliveredAsync(trainer.Email, "Welcome to TrainerSphere");
        message.From.ShouldBe(new MailpitAddress("TrainerSphere", "no-reply@trainersphere.dev"));
        message.To.ShouldHaveSingleItem().ShouldBe(new MailpitAddress($"{trainer.FirstName} {trainer.LastName}", trainer.Email));
        message.Text.ShouldContain("http://web.test/login");
    }

    [Fact]
    public async Task ClientJoinedEmail_WithNonAsciiNames_KeepsThemInSubjectAndRecipient()
    {
        TestUser trainer = await CreateTrainerAsync(b => b.WithName("Željko", "Đurić"));

        TestUser client = await CreateClientAsync(trainer, a => a.WithFirstName("Zoë").WithLastName("Ångström"));

        MailpitMessage message = await SingleDeliveredAsync(trainer.Email, "Zoë Ångström accepted your invitation");
        message.To.ShouldHaveSingleItem().ShouldBe(new MailpitAddress("Željko Đurić", trainer.Email));
        message.Text.ShouldContain($"Zoë Ångström ({client.Email})");
        message.Text.ShouldContain($"http://web.test/clients/{client.Id}");
    }

    private async Task<MailpitMessage> SingleDeliveredAsync(string to, string subject)
    {
        using HttpClient mailpit = new() { BaseAddress = _mailpit.MailpitApi };
        string query = Uri.EscapeDataString($"to:{to} subject:\"{subject}\"");
        MailpitSummary? summary = null;

        await NotificationFlows.WaitUntilAsync(
            async () =>
            {
                MailpitSearch search = (await mailpit.GetFromJsonAsync<MailpitSearch>($"/api/v1/search?query={query}", HttpAssertions.Json, Ct))!;
                summary = search.Messages.SingleOrDefault(m => m.Subject == subject);
                return summary is not null;
            },
            $"Mailpit has '{subject}' for {to}",
            TimeSpan.FromSeconds(5));

        return (await mailpit.GetFromJsonAsync<MailpitMessage>($"/api/v1/message/{summary!.Id}", HttpAssertions.Json, Ct))!;
    }

    private sealed record MailpitSearch(IReadOnlyList<MailpitSummary> Messages);

    private sealed record MailpitSummary(string Id, string Subject);

    private sealed record MailpitAddress(string Name, string Address);

    private sealed record MailpitMessage(MailpitAddress From, IReadOnlyList<MailpitAddress> To, string Subject, string Text, string Html);
}
