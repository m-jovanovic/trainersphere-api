namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the Mailersend email request.
/// </summary>
/// <param name="From">The from.</param>
/// <param name="To">The to array.</param>
/// <param name="Variables">The variables.</param>
/// <param name="TemplateId">The template identifier.</param>
internal sealed record EmailRequest(Sender From, Recipient[] To, Variable[] Variables, string TemplateId);
