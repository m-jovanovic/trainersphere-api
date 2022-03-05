using Microsoft.Extensions.Options;
using Modules.Notifications.Application.Abstractions.Email;
using Modules.Notifications.Infrastructure.Email.Abstractions;
using Modules.Notifications.Infrastructure.Email.Configuration;
using Modules.Notifications.Infrastructure.Email.Contracts;

namespace Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Represents the email sender.
/// </summary>
internal sealed class EmailSender : IEmailSender
{
    private readonly IMailersendClient _mailersendClient;
    private readonly MailersendOptions _options;

    /// <summary>
    /// Initializes a new instance of the <see cref="EmailSender"/> class.
    /// </summary>
    /// <param name="mailersendClient">The mailersend client.</param>
    /// <param name="options">The options.</param>
    public EmailSender(IMailersendClient mailersendClient, IOptions<MailersendOptions> options)
    {
        _mailersendClient = mailersendClient;
        _options = options.Value;
    }

    /// <inheritdoc />
    public async Task SendWelcomeAsync(WelcomeEmailRequest welcomeEmailRequest, CancellationToken cancellationToken = default)
    {
        // Add some sort of builder pattern here.
        var emailRequest = new EmailRequest(
            new Sender(_options.SenderEmail),
            new[]
            {
                new Recipient(welcomeEmailRequest.Email)
            },
            new Variables(
                new Recipient(welcomeEmailRequest.Email),
                new[]
                {
                    new Substitution("name", welcomeEmailRequest.Name)
                }),
            _options.Templates.WelcomeEmail);

        await _mailersendClient.SendEmailAsync(emailRequest, cancellationToken);
    }
}
