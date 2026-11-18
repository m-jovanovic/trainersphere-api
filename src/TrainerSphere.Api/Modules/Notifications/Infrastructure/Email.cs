using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Net.Mail;
using System.Net.Mime;
using System.Text;
using Microsoft.Extensions.Options;
using TrainerSphere.Api.Modules.Notifications.Domain;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Notifications.Infrastructure;

internal interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}

internal static class EmailAddresses
{
    public static bool TryParse(string? value, [NotNullWhen(true)] out MailAddress? address) =>
        MailAddress.TryCreate(value, out address);
}

internal sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string Host { get; set; } = "localhost";

    public int Port { get; set; } = 1025;

    public string From { get; set; } = "TrainerSphere <no-reply@trainersphere.dev>";

    public bool EnableSsl { get; set; }

    public string? UserName { get; set; }

    public string? Password { get; set; }

    public int TimeoutSeconds { get; set; } = 30;
}

internal sealed class WebLinkOptions
{
    public const string SectionName = "App";

    public string WebBaseUrl { get; set; } = "http://localhost:5173";
}

/// <summary>
/// Links into the web app, which owns these routes: <c>/invite/$token</c>, <c>/reset-password</c>, <c>/login</c>, <c>/clients/$id</c>.
/// </summary>
internal sealed class WebLinks(IOptions<WebLinkOptions> options)
{
    private string Base => options.Value.WebBaseUrl.TrimEnd('/');

    public string Invitation(string token) => $"{Base}/invite/{Uri.EscapeDataString(token)}";

    public string PasswordReset(string token) => $"{Base}/reset-password?token={Uri.EscapeDataString(token)}";

    public string Login() => $"{Base}/login";

    public string Client(Guid clientId) => $"{Base}/clients/{clientId}";
}

/// <summary>
/// Plain SMTP delivery (Mailpit locally). Failures throw, so the inbox retries the message.
/// </summary>
[SuppressMessage("Security", "S5332:Using clear-text protocols is security-sensitive", Justification = "TLS is configured through Smtp:EnableSsl; the local Mailpit relay has none.")]
internal sealed class SmtpEmailSender(IOptions<SmtpOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        SmtpOptions settings = options.Value;
        MailAddress from = new(settings.From);

        using SmtpClient client = new(settings.Host, settings.Port)
        {
            EnableSsl = settings.EnableSsl,
            DeliveryMethod = SmtpDeliveryMethod.Network,
            Timeout = settings.TimeoutSeconds * 1000,
        };

        if (!string.IsNullOrEmpty(settings.UserName))
        {
            client.Credentials = new NetworkCredential(settings.UserName, settings.Password);
        }

        using MailMessage mail = new()
        {
            From = from,
            Subject = message.Subject,
            SubjectEncoding = Encoding.UTF8,
            Body = message.TextBody,
            BodyEncoding = Encoding.UTF8,
            IsBodyHtml = false,
        };
        mail.To.Add(new MailAddress(message.To, message.ToName));
        mail.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(message.HtmlBody, Encoding.UTF8, MediaTypeNames.Text.Html));

        await client.SendMailAsync(mail, cancellationToken);
    }
}

/// <summary>
/// Sends an email and records it in the log inside the handler's transaction. Delivery is at least once: if the
/// transaction fails after the SMTP hand-off, the retry sends the email again.
/// </summary>
internal sealed class EmailDispatcher(IEmailSender sender, IDbSession db, TimeProvider timeProvider)
{
    public async Task SendAsync(EmailMessage message, Guid sourceEventId, CancellationToken cancellationToken)
    {
        await sender.SendAsync(message, cancellationToken);
        await db.ExecuteAsync(
            """
            INSERT INTO notifications.email_log (id, source_event_id, kind, recipient, subject, sent_at)
            VALUES (@Id, @SourceEventId, @Kind, @Recipient, @Subject, @SentAt)
            """,
            new
            {
                Id = Ids.New(timeProvider),
                SourceEventId = sourceEventId,
                message.Kind,
                Recipient = message.To,
                message.Subject,
                SentAt = timeProvider.UtcNow(),
            },
            cancellationToken);
    }
}

/// <summary>
/// One shared look for every email: plain text plus a simple inline-styled HTML card. All dynamic text is encoded.
/// </summary>
internal sealed class EmailBuilder
{
    private readonly List<string> _paragraphs = [];
    private (string Label, string Url)? _action;
    private string? _quote;
    private string? _footnote;

    public EmailBuilder(string heading) => Heading = heading;

    public string Heading { get; }

    public static string FormatUtc(DateTime value) =>
        value.ToString("d MMM yyyy 'at' HH:mm 'UTC'", CultureInfo.InvariantCulture);

    public EmailBuilder Paragraph(string text)
    {
        _paragraphs.Add(text);
        return this;
    }

    public EmailBuilder Quote(string? text)
    {
        _quote = string.IsNullOrWhiteSpace(text) ? null : text.Trim();
        return this;
    }

    public EmailBuilder Action(string label, string url)
    {
        _action = (label, url);
        return this;
    }

    public EmailBuilder Footnote(string text)
    {
        _footnote = text;
        return this;
    }

    public EmailMessage Build(string kind, string to, string toName, string subject) =>
        new(kind, to, toName, subject, BuildText(), BuildHtml());

    private static string Encode(string value) => WebUtility.HtmlEncode(value);

    private string BuildText()
    {
        StringBuilder text = new();
        text.AppendLine(Heading).AppendLine();

        foreach (string paragraph in _paragraphs)
        {
            text.AppendLine(paragraph).AppendLine();
        }

        if (_quote is not null)
        {
            text.Append("> ").AppendLine(_quote.ReplaceLineEndings("\n> ")).AppendLine();
        }

        if (_action is { } action)
        {
            text.Append(action.Label).AppendLine(":").AppendLine(action.Url).AppendLine();
        }

        if (_footnote is not null)
        {
            text.AppendLine(_footnote).AppendLine();
        }

        text.Append("TrainerSphere");
        return text.ToString();
    }

    private string BuildHtml()
    {
        StringBuilder html = new();
        html.Append("<!DOCTYPE html><html><body style=\"margin:0;padding:24px;background:#f4f4f5;font-family:Segoe UI,Helvetica,Arial,sans-serif;color:#18181b\">")
            .Append("<div style=\"max-width:560px;margin:0 auto;background:#ffffff;border-radius:12px;padding:32px\">")
            .Append("<p style=\"margin:0 0 24px;font-weight:700;color:#0d9488\">TrainerSphere</p>")
            .Append("<h1 style=\"margin:0 0 16px;font-size:22px\">").Append(Encode(Heading)).Append("</h1>");

        foreach (string paragraph in _paragraphs)
        {
            html.Append("<p style=\"margin:0 0 16px;line-height:1.5\">").Append(Encode(paragraph)).Append("</p>");
        }

        if (_quote is not null)
        {
            html.Append("<blockquote style=\"margin:0 0 16px;padding:12px 16px;border-left:4px solid #0d9488;background:#f0fdfa\">")
                .Append(Encode(_quote).ReplaceLineEndings("<br>"))
                .Append("</blockquote>");
        }

        if (_action is { } action)
        {
            string url = Encode(action.Url);
            html.Append("<p style=\"margin:24px 0\"><a href=\"").Append(url)
                .Append("\" style=\"display:inline-block;padding:12px 20px;background:#0d9488;color:#ffffff;text-decoration:none;border-radius:8px;font-weight:600\">")
                .Append(Encode(action.Label)).Append("</a></p>")
                .Append("<p style=\"margin:0 0 16px;font-size:13px;color:#52525b\">Or open this link: <a href=\"").Append(url).Append("\">").Append(url).Append("</a></p>");
        }

        if (_footnote is not null)
        {
            html.Append("<p style=\"margin:0;font-size:13px;color:#52525b\">").Append(Encode(_footnote)).Append("</p>");
        }

        html.Append("</div></body></html>");
        return html.ToString();
    }
}
