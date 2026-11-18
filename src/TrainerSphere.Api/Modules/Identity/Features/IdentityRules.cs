using FluentValidation;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.SharedKernel.Domain;

namespace TrainerSphere.Api.Modules.Identity.Features;

internal static class IdentityRules
{
    public const int PasswordMinLength = 8;
    public const int PasswordMaxLength = 128;
    public const int NameMaxLength = 100;

    public static IRuleBuilderOptions<T, string> ValidEmail<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().MaximumLength(EmailAddress.MaxLength).EmailAddress();

    public static IRuleBuilderOptions<T, string> ValidPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().Length(PasswordMinLength, PasswordMaxLength);

    public static IRuleBuilderOptions<T, string> ValidName<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("Must not be empty.")
            .MaximumLength(NameMaxLength);

    public static IRuleBuilderOptions<T, string?> ValidTimeZone<T>(this IRuleBuilder<T, string?> rule) =>
        rule.Must(zone => zone is null || TimeZones.IsValid(zone)).WithMessage("Must be a known IANA time zone, for example Europe/Belgrade.");
}
