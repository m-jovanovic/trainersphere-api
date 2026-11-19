using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerSphere.SharedKernel.Http;

/// <summary>
/// Converts enum values to and from the kebab-case strings used on the wire (<c>InProgress</c> = <c>in-progress</c>).
/// A <see cref="JsonStringEnumMemberNameAttribute"/> overrides the derived name (<c>Percent1Rm</c> = <c>percent-1rm</c>).
/// </summary>
public static class KebabCase
{
    public static string ToKebab<TEnum>(this TEnum value)
        where TEnum : struct, Enum =>
        ((Enum)value).ToKebab();

    public static string ToKebab(this Enum value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string name = value.ToString();
        return value.GetType().GetField(name)?.GetCustomAttribute<JsonStringEnumMemberNameAttribute>()?.Name
            ?? JsonNamingPolicy.KebabCaseLower.ConvertName(name);
    }

    public static bool TryParse<TEnum>(string? text, out TEnum value)
        where TEnum : struct, Enum
    {
        TEnum[] matches = [.. Enum.GetValues<TEnum>().Where(candidate => string.Equals(candidate.ToKebab(), text, StringComparison.Ordinal))];
        value = matches.Length == 1 ? matches[0] : default;
        return matches.Length == 1;
    }

    public static string Allowed<TEnum>()
        where TEnum : struct, Enum =>
        string.Join(", ", Enum.GetValues<TEnum>().Select(v => v.ToKebab()));
}
