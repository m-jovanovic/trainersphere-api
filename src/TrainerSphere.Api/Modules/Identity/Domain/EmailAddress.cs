namespace TrainerSphere.Api.Modules.Identity.Domain;

internal static class EmailAddress
{
    public const int MaxLength = 254;

    // Emails are compared case-insensitively everywhere, so they are stored lower-cased.
    public static string Normalize(string email) => email.Trim().ToLowerInvariant();
}
