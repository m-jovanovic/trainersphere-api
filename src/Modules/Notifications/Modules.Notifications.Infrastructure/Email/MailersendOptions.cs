namespace Modules.Notifications.Infrastructure.Email;

/// <summary>
/// Represents the Mailersend options.
/// </summary>
internal sealed class MailersendOptions
{
    /// <summary>
    /// Gets the base URL.
    /// </summary>
    public string BaseUrl { get; init; } = string.Empty;

    /// <summary>
    /// Gets the access token.
    /// </summary>
    public string AccessToken { get; init; } = string.Empty;
}
