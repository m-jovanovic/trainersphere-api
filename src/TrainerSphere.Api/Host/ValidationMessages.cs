using System.Collections.Frozen;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json;
using FluentValidation;
using FluentValidation.Resources;

namespace TrainerSphere.Api.Host;

/// <summary>
/// Validation messages are shown next to the field they are keyed by, so they never name the property:
/// "Must be between 1 and 100.", not "'Page Size' must be between 1 and 100.".
/// </summary>
internal sealed class ValidationMessages : LanguageManager
{
    private static readonly FrozenDictionary<string, string> Messages = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["AsyncPredicateValidator"] = "Is not valid.",
        ["CreditCardValidator"] = "Must be a valid credit card number.",
        ["EmailValidator"] = "Must be a valid email address.",
        ["EmptyValidator"] = "Must be empty.",
        ["EnumValidator"] = "Is not an allowed value.",
        ["EqualValidator"] = "Must be equal to {ComparisonValue}.",
        ["ExactLengthValidator"] = "Must be exactly {MaxLength} characters.",
        ["ExclusiveBetweenValidator"] = "Must be greater than {From} and less than {To}.",
        ["GreaterThanOrEqualValidator"] = "Must be at least {ComparisonValue}.",
        ["GreaterThanValidator"] = "Must be greater than {ComparisonValue}.",
        ["InclusiveBetweenValidator"] = "Must be between {From} and {To}.",
        ["LengthValidator"] = "Must be between {MinLength} and {MaxLength} characters.",
        ["LessThanOrEqualValidator"] = "Must be at most {ComparisonValue}.",
        ["LessThanValidator"] = "Must be less than {ComparisonValue}.",
        ["MaximumLengthValidator"] = "Must be at most {MaxLength} characters.",
        ["MinimumLengthValidator"] = "Must be at least {MinLength} characters.",
        ["NotEmptyValidator"] = "Must not be empty.",
        ["NotEqualValidator"] = "Must not be equal to {ComparisonValue}.",
        ["NotNullValidator"] = "Must not be empty.",
        ["NullValidator"] = "Must be empty.",
        ["PredicateValidator"] = "Is not valid.",
        ["RegularExpressionValidator"] = "Is not in the correct format.",
        ["ScalePrecisionValidator"] = "Must have at most {ExpectedPrecision} digits in total, with at most {ExpectedScale} decimals.",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    public override string GetString(string key, CultureInfo? culture = null) =>
        Messages.TryGetValue(key, out string? message) ? message : base.GetString(key, culture);

    /// <summary>
    /// Runs when the assembly loads, so validators built by hand (tests) get the same messages as the host.
    /// </summary>
    [ModuleInitializer]
    internal static void Install()
    {
        ValidatorOptions.Global.LanguageManager = new ValidationMessages { Culture = CultureInfo.InvariantCulture };

        // Comparisons against another property ({ComparisonValue}) name it the way the client sends it.
        ValidatorOptions.Global.DisplayNameResolver = (_, member, _) =>
            member is null ? null : JsonNamingPolicy.CamelCase.ConvertName(member.Name);
    }
}
