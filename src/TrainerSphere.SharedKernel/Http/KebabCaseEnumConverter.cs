using System.Collections.Frozen;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrainerSphere.SharedKernel.Http;

/// <summary>
/// A JSON value was rejected with a message that is safe to return to API clients as a field error.
/// </summary>
public sealed class JsonValueException : JsonException
{
    public JsonValueException()
    {
    }

    public JsonValueException(string message)
        : base(message)
    {
    }

    public JsonValueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Serializes every enum as its kebab-case name and reads only the exact kebab-case names: no integers, no other casing.
/// </summary>
public sealed class KebabCaseEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        ArgumentNullException.ThrowIfNull(typeToConvert);

        return typeToConvert.IsEnum;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
        (JsonConverter)Activator.CreateInstance(typeof(KebabCaseEnumConverter<>).MakeGenericType(typeToConvert))!;
}

/// <inheritdoc cref="KebabCaseEnumConverterFactory"/>
public sealed class KebabCaseEnumConverter<TEnum> : JsonConverter<TEnum>
    where TEnum : struct, Enum
{
    private static readonly FrozenDictionary<TEnum, string> Names =
        Enum.GetValues<TEnum>().Distinct().ToFrozenDictionary(value => value, value => value.ToKebab());

    private static readonly FrozenDictionary<string, TEnum> Values =
        Names.ToFrozenDictionary(pair => pair.Value, pair => pair.Key, StringComparer.Ordinal);

    private static readonly string InvalidMessage = $"Must be one of: {string.Join(", ", Names.Values)}.";

    public override TEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.TokenType == JsonTokenType.String ? Parse(reader.GetString()) : throw new JsonValueException(InvalidMessage);

    public override void Write(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStringValue(Name(value));
    }

    public override TEnum ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        Parse(reader.GetString());

    public override void WriteAsPropertyName(Utf8JsonWriter writer, TEnum value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WritePropertyName(Name(value));
    }

    private static TEnum Parse(string? text) =>
        text is not null && Values.TryGetValue(text, out TEnum value) ? value : throw new JsonValueException(InvalidMessage);

    private static string Name(TEnum value) =>
        Names.TryGetValue(value, out string? name) ? name : throw new JsonException($"{typeof(TEnum).Name} has no member with value {value}.");
}
