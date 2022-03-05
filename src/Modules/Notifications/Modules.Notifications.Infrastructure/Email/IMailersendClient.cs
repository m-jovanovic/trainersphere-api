using Refit;

namespace Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Represents the Mailersend client interface.
/// </summary>
internal interface IMailersendClient
{
    [Post("/email")]
    Task SendEmailAsync(object request, CancellationToken cancellationToken = default);
}
