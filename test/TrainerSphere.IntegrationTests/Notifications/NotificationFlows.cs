using System.Net;
using System.Text.RegularExpressions;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Notifications;

internal sealed record EmailLogRow(Guid Id, Guid SourceEventId, string Kind, string Recipient, string Subject, DateTime SentAt);

internal sealed record InboxRow(int Attempts, string? Error, DateTime? ProcessedAt);

internal static partial class NotificationFlows
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public static async Task RequestPasswordResetAsync(this ApiFactory factory, string email)
    {
        await (await factory.CreateAnonymousClient().PostJsonAsync(IdentityRoutes.ForgotPassword, new ForgotPasswordRequest(email)))
            .ShouldHaveStatusAsync(HttpStatusCode.Accepted);
        await factory.WaitForMessagesAsync();
    }

    public static Task<IReadOnlyList<EmailLogRow>> EmailLogAsync(this ApiFactory factory, string recipient) =>
        factory.QueryAsync<EmailLogRow>(
            """
            SELECT id AS "Id", source_event_id AS "SourceEventId", kind AS "Kind", recipient AS "Recipient", subject AS "Subject", sent_at AS "SentAt"
            FROM notifications.email_log WHERE recipient = @Recipient ORDER BY sent_at
            """,
            new { Recipient = recipient });

    /// <summary>
    /// The Notifications inbox row of an integration event (its id is the message id).
    /// </summary>
    public static Task<InboxRow> InboxMessageAsync(this ApiFactory factory, Guid eventId) =>
        factory.QuerySingleAsync<InboxRow>(
            """SELECT attempts AS "Attempts", error AS "Error", processed_at AS "ProcessedAt" FROM notifications.inbox_messages WHERE id = @Id""",
            new { Id = eventId });

    public static async Task WaitUntilAsync(Func<Task<bool>> condition, string what, TimeSpan? timeout = null)
    {
        DateTime deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(15));

        while (!await condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new ShouldAssertException($"Timed out waiting until {what}.");
            }

            await Task.Delay(25, Ct);
        }
    }

    public static string InvitationToken(this EmailMessage email, string webBaseUrl = "http://web.test")
    {
        Match link = new Regex(Regex.Escape(webBaseUrl) + "/invite/(?<token>[A-Za-z0-9_-]+)").Match(email.TextBody);
        link.Success.ShouldBeTrue($"No invitation link in:{Environment.NewLine}{email.TextBody}");
        return link.Groups["token"].Value;
    }

    public static string ResetToken(this EmailMessage email) => ResetToken(email.TextBody);

    public static string ResetToken(string text)
    {
        Match link = ResetLink().Match(text);
        link.Success.ShouldBeTrue($"No reset link in:{Environment.NewLine}{text}");
        return Uri.UnescapeDataString(link.Groups["token"].Value);
    }

    /// <summary>
    /// The UTC expiry as the emails print it.
    /// </summary>
    public static string EmailDate(DateTimeOffset value) =>
        value.UtcDateTime.ToString("d MMM yyyy 'at' HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);

    [GeneratedRegex(@"http://web\.test/reset-password\?token=(?<token>[A-Za-z0-9_\-%]+)")]
    private static partial Regex ResetLink();
}
