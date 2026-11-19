using System.Collections.Concurrent;
using System.Net.Mail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.IntegrationTests.Notifications;

/// <summary>
/// The host with an email sender that can be told to fail, as an SMTP outage would.
/// </summary>
public sealed class FlakyEmailApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    public const int MaxAttempts = 3;

    private readonly ConcurrentDictionary<string, int> _failures = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Makes the next <paramref name="count"/> sends to <paramref name="email"/> throw.
    /// </summary>
    public void FailNextSends(string email, int count) => _failures[email] = count;

    protected override void ConfigureSettings(IDictionary<string, string?> settings) =>
        settings["Messaging:MaxAttempts"] = MaxAttempts.ToString(System.Globalization.CultureInfo.InvariantCulture);

    protected override void ConfigureServices(IServiceCollection services)
    {
        services.RemoveAll<IEmailSender>();
        services.AddSingleton<IEmailSender>(new FlakySender(Emails, _failures));
    }

    private sealed class FlakySender(IEmailSender inner, ConcurrentDictionary<string, int> failures) : IEmailSender
    {
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            if (failures.TryGetValue(message.To, out int left) && left > 0)
            {
                failures[message.To] = left - 1;
                throw new SmtpException(SmtpStatusCode.MailboxBusy, "Mailbox busy (simulated outage).");
            }

            return inner.SendAsync(message, cancellationToken);
        }
    }
}

/// <summary>
/// Delivery is at least once and handlers are idempotent per event: redelivered or reprocessed events never send
/// an email twice, and a failed send is retried.
/// </summary>
public sealed class EmailDeliveryTests(FlakyEmailApiFactory factory) : IntegrationTest(factory), IClassFixture<FlakyEmailApiFactory>
{
    private readonly FlakyEmailApiFactory _flaky = factory;

    [Fact]
    public async Task ReprocessedInboxMessage_DoesNotSendTheEmailAgain()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();
        Guid eventId = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == issued.Invitation.Id).Id;

        // As if the process died after the handler committed but before the message was marked processed.
        await Factory.ExecuteAsync("UPDATE notifications.inbox_messages SET processed_at = NULL WHERE id = @Id", new { Id = eventId });
        await Factory.WaitForMessagesAsync();

        (await Factory.InboxMessageAsync(eventId)).ProcessedAt.ShouldNotBeNull();
        Factory.Emails.To(issued.Invitation.Email).ShouldHaveSingleItem();
        (await Factory.EmailLogAsync(issued.Invitation.Email)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task RepublishedEvent_DoesNotSendTheEmailAgain()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();
        TrainerRegistered original = Factory.Events.Last<TrainerRegistered>(e => e.UserId == trainer.Id);

        await using (AsyncServiceScope scope = Factory.Services.CreateAsyncScope())
        {
            IDbSession session = scope.ServiceProvider.GetRequiredService<IDbSession>();
            await session.BeginTransactionAsync(Ct);
            await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(original, Ct);
            await session.CommitAsync(Ct);
        }

        await Factory.WaitForMessagesAsync();

        Factory.Emails.To(trainer.Email).ShouldHaveSingleItem();
        (await Factory.EmailLogAsync(trainer.Email)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task FailedSend_IsRetriedAndDeliveredOnce()
    {
        TestUser trainer = await CreateTrainerAsync();
        string invitee = Unique.Email("flaky");
        _flaky.FailNextSends(invitee, 1);

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithEmail(invitee));
        Guid eventId = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == issued.Invitation.Id).Id;
        await NotificationFlows.WaitUntilAsync(async () => (await Factory.InboxMessageAsync(eventId)).ProcessedAt is not null, "the invitation email is sent");

        InboxRow message = await Factory.InboxMessageAsync(eventId);
        message.Attempts.ShouldBe(1);
        message.Error.ShouldBeNull();
        Factory.Emails.To(invitee).ShouldHaveSingleItem();
        (await Factory.EmailLogAsync(invitee)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task SendThatKeepsFailing_StopsAfterMaxAttemptsAndLogsNothing()
    {
        TestUser trainer = await CreateTrainerAsync();
        string invitee = Unique.Email("bouncing");
        _flaky.FailNextSends(invitee, int.MaxValue);

        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithEmail(invitee));
        Guid eventId = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == issued.Invitation.Id).Id;

        try
        {
            await NotificationFlows.WaitUntilAsync(async () => (await Factory.InboxMessageAsync(eventId)).Attempts == FlakyEmailApiFactory.MaxAttempts, "the message is given up");

            InboxRow message = await Factory.InboxMessageAsync(eventId);
            message.ProcessedAt.ShouldBeNull();
            message.Error.ShouldNotBeNull().ShouldContain(nameof(SmtpException));
            Factory.Emails.To(invitee).ShouldBeEmpty();
            (await Factory.EmailLogAsync(invitee)).ShouldBeEmpty();
        }
        finally
        {
            await ClearDeadMessagesAsync();
        }
    }

    [Fact]
    public async Task WelcomeEmailOutage_StillNotifiesTheTrainerWhenAClientJoins()
    {
        string email = Unique.Email("outage");
        _flaky.FailNextSends(email, FlakyEmailApiFactory.MaxAttempts);
        TestUser trainer = await CreateTrainerAsync(b => b.WithEmail(email));
        Guid welcomeId = Factory.Events.Last<TrainerRegistered>(e => e.UserId == trainer.Id).Id;

        try
        {
            await NotificationFlows.WaitUntilAsync(async () => (await Factory.InboxMessageAsync(welcomeId)).Attempts == FlakyEmailApiFactory.MaxAttempts, "the welcome email is given up");

            IssuedInvitation issued = await InviteAsync(trainer);
            await Factory.AcceptInvitationAsync(issued.Token);
            Guid joinedId = Factory.Events.Last<ClientRegistered>(e => e.InvitationId == issued.Invitation.Id).Id;

            await NotificationFlows.WaitUntilAsync(
                async () => await Factory.InboxMessageAsync(joinedId) is { ProcessedAt: not null } or { Attempts: FlakyEmailApiFactory.MaxAttempts },
                "the client-joined message is handled or given up");

            Factory.Emails.To(email).ShouldContain(m => m.Kind == EmailKinds.ClientJoined);
        }
        finally
        {
            await ClearDeadMessagesAsync();
        }
    }

    // Given-up messages would make every later WaitForMessagesAsync in this fixture fail.
    private Task<int> ClearDeadMessagesAsync() =>
        Factory.ExecuteAsync("UPDATE notifications.inbox_messages SET processed_at = now() WHERE processed_at IS NULL AND error IS NOT NULL");
}
