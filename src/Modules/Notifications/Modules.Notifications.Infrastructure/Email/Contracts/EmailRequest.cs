namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the Mailersend email request.
/// </summary>
/// <param name="From">The from.</param>
/// <param name="To">The to array.</param>
/// <param name="Variables">The variables.</param>
/// <param name="Template_Id">The template identifier.</param>
internal sealed record EmailRequest(Sender From, Recipient[] To, Variables Variables, string Template_Id);
