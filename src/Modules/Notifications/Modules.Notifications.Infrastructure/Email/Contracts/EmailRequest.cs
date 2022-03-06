using Shared.Extensions;

namespace Modules.Notifications.Infrastructure.Email.Contracts;

/// <summary>
/// Represents the Mailersend email request.
/// </summary>
internal sealed record EmailRequest
{
    private EmailRequest()
    {
    }

    /// <summary>
    /// Gets the from.
    /// </summary>
    public Sender From { get; init; } = new(string.Empty);

    /// <summary>
    /// Gets the the to array.
    /// </summary>
    public Recipient[] To { get; init; } = Array.Empty<Recipient>();

    /// <summary>
    /// Gets the variables.
    /// </summary>
    public Variable[] Variables { get; init; } = Array.Empty<Variable>();

    /// <summary>
    /// Gets the template identifier.
    /// </summary>
    public string TemplateId { get; init; } = string.Empty;

    public static EmailRequest Create() => new();

    public EmailRequest WithSender(string sender) => this with
    {
        From = new Sender(sender)
    };

    public EmailRequest WithRecipient(string email) => this with
    {
        To = new List<Recipient> { new(email) }
            .Tap(list => list.AddRange(To))
            .ToArray()
    };

    public EmailRequest WithVariable(Variable variable) => this with
    {
        Variables = new List<Variable> { variable }
            .Tap(list => list.AddRange(Variables))
            .ToArray()
    };

    public EmailRequest WithTemplateId(string templateId) => this with
    {
        TemplateId = templateId
    };
}
