using System.Collections.Concurrent;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Replaces SMTP delivery in <see cref="ApiFactory"/>: every email the Notifications module sends lands here.
/// Emails are sent asynchronously, so call <see cref="ApiFactory.WaitForMessagesAsync"/> before asserting.
/// </summary>
internal sealed class CapturedEmails : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    public IReadOnlyList<EmailMessage> All => [.. _sent];

    public IReadOnlyList<EmailMessage> To(string email) =>
        [.. _sent.Where(m => string.Equals(m.To, email, StringComparison.OrdinalIgnoreCase))];

    /// <summary>
    /// The only email of <paramref name="kind"/> (see <see cref="EmailKinds"/>) sent to <paramref name="email"/>.
    /// </summary>
    public EmailMessage Single(string email, string kind) => To(email).Where(m => m.Kind == kind).ShouldHaveSingleItem();

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
