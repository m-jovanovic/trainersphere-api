using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
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

        using HttpClient mailpit = new() { BaseAddress = _mailpit.MailpitApi };
        MailpitSearch search = (await mailpit.GetFromJsonAsync<MailpitSearch>($"/api/v1/search?query={Uri.EscapeDataString($"to:{issued.Invitation.Email}")}", HttpAssertions.Json, Ct))!;
        MailpitSummary summary = search.Messages.ShouldHaveSingleItem();
        summary.Subject.ShouldBe($"{trainer.FirstName} {trainer.LastName} invited you to TrainerSphere");

        MailpitMessage message = (await mailpit.GetFromJsonAsync<MailpitMessage>($"/api/v1/message/{summary.Id}", HttpAssertions.Json, Ct))!;
        message.From.Address.ShouldBe("no-reply@trainersphere.dev");
        message.Text.ShouldContain($"http://web.test/invite/{issued.Token}");
        message.Html.ShouldContain($"href=\"http://web.test/invite/{issued.Token}\"");
    }

    private sealed record MailpitSearch(IReadOnlyList<MailpitSummary> Messages);

    private sealed record MailpitSummary(string Id, string Subject);

    private sealed record MailpitAddress(string Address, string Name);

    private sealed record MailpitMessage(MailpitAddress From, string Subject, string Text, string Html);
}
