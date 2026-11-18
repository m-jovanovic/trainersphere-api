namespace TrainerSphere.Api.Modules.Notifications.Domain;

internal static class EmailKinds
{
    public const string Invitation = "invitation";
    public const string TrainerWelcome = "trainer-welcome";
    public const string ClientJoined = "client-joined";
    public const string PasswordReset = "password-reset";
}

/// <summary>
/// A rendered email. <see cref="Kind"/> is one of <see cref="EmailKinds"/> and is recorded in the email log.
/// </summary>
internal sealed record EmailMessage(string Kind, string To, string ToName, string Subject, string TextBody, string HtmlBody);

/// <summary>
/// The Notifications module's own copy of the people it can email, kept in sync from Identity events.
/// </summary>
internal sealed record Recipient(Guid UserId, string Email, string FirstName, string LastName, string Role, Guid? TrainerId)
{
    public const string TrainerRole = "Trainer";
    public const string ClientRole = "Client";

    public string FullName => $"{FirstName} {LastName}";
}
