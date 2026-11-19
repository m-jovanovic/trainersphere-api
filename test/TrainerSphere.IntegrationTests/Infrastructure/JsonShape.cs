using System.Text.Json;

namespace TrainerSphere.IntegrationTests.Infrastructure;

/// <summary>
/// Assertions on the raw JSON shape: the contract says every documented property is always present (null when
/// absent) and timestamps are UTC, which typed deserialization cannot tell apart from missing members.
/// </summary>
public static class JsonShape
{
    public static async Task<JsonElement> ReadJsonAsync(this HttpResponseMessage response)
    {
        ArgumentNullException.ThrowIfNull(response);

        string body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Asserts the object has exactly these properties: none missing, none extra.
    /// </summary>
    public static JsonElement ShouldHaveExactProperties(this JsonElement element, params string[] expected)
    {
        element.ValueKind.ShouldBe(JsonValueKind.Object);
        string[] actual = [.. element.EnumerateObject().Select(p => p.Name)];

        actual.Except(expected, StringComparer.Ordinal).ShouldBeEmpty($"Properties not in the contract: {element}");
        expected.Except(actual, StringComparer.Ordinal).ShouldBeEmpty($"Contract properties missing: {element}");
        return element;
    }

    /// <summary>
    /// Asserts two deserialized values are equal by their JSON, for records whose list members make record
    /// equality reference-based. Numbers compare numerically: reads serialize some decimals with trailing zeros
    /// ("60.00") where writes return "60", which is the same JSON number.
    /// </summary>
    public static void ShouldBeJson<T>(this T actual, T expected)
    {
        using var actualDocument = JsonDocument.Parse(JsonSerializer.Serialize(actual, HttpAssertions.Json));
        using var expectedDocument = JsonDocument.Parse(JsonSerializer.Serialize(expected, HttpAssertions.Json));
        if (!JsonEquals(actualDocument.RootElement, expectedDocument.RootElement))
        {
            actualDocument.RootElement.GetRawText().ShouldBe(expectedDocument.RootElement.GetRawText());
        }
    }

    public static JsonElement Property(this JsonElement element, string path)
    {
        JsonElement current = element;

        foreach (string segment in path.Split('.'))
        {
            current = current.GetProperty(segment);
        }

        return current;
    }

    public static void ShouldBeNullProperty(this JsonElement element, string name) =>
        element.GetProperty(name).ValueKind.ShouldBe(JsonValueKind.Null, $"'{name}' must be present and null.");

    /// <summary>
    /// Asserts an ISO 8601 UTC timestamp (<c>...Z</c>).
    /// </summary>
    public static void ShouldBeUtcTimestamp(this JsonElement element, string name)
    {
        string? value = element.GetProperty(name).GetString();
        value.ShouldNotBeNull($"'{name}' must be a timestamp string.");
        value.ShouldEndWith("Z", Case.Sensitive, $"'{name}' must be UTC: {value}");
        DateTimeOffset.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, out _).ShouldBeTrue(value);
    }

    private static bool JsonEquals(JsonElement actual, JsonElement expected) =>
        (actual.ValueKind, expected.ValueKind) switch
        {
            (JsonValueKind.Number, JsonValueKind.Number) => actual.GetDecimal() == expected.GetDecimal(),
            (JsonValueKind.Object, JsonValueKind.Object) =>
                actual.EnumerateObject().Count() == expected.EnumerateObject().Count() &&
                actual.EnumerateObject().All(p => expected.TryGetProperty(p.Name, out JsonElement match) && JsonEquals(p.Value, match)),
            (JsonValueKind.Array, JsonValueKind.Array) =>
                actual.GetArrayLength() == expected.GetArrayLength() &&
                actual.EnumerateArray().Zip(expected.EnumerateArray()).All(pair => JsonEquals(pair.First, pair.Second)),
            _ => actual.GetRawText() == expected.GetRawText(),
        };
}
