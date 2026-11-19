using System.Text.Json;
using System.Text.RegularExpressions;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Section 2: timestamps have at most microsecond precision, so the value a write returns equals what later reads return.
/// </summary>
public static partial class TimestampPrecision
{
    public static void ShouldHaveAtMostMicroseconds(this JsonElement element, string name)
    {
        string? value = element.GetProperty(name).GetString();
        value.ShouldNotBeNull($"'{name}' must be a timestamp string.");
        AtMostSixFractionalDigits().IsMatch(value).ShouldBeTrue($"'{name}' has more than microsecond precision: {value}");
    }

    [GeneratedRegex(@"T\d{2}:\d{2}:\d{2}(\.\d{1,6})?Z$")]
    private static partial Regex AtMostSixFractionalDigits();
}
