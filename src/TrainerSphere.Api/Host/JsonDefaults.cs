using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TrainerSphere.SharedKernel.Http;

namespace TrainerSphere.Api.Host;

internal static class JsonDefaults
{
    public static void Configure(JsonSerializerOptions options)
    {
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
        options.Converters.Add(new KebabCaseEnumConverterFactory());
        options.TypeInfoResolver = (options.TypeInfoResolver ?? new DefaultJsonTypeInfoResolver()).WithAddedModifier(RequireValueTypeParameters);
    }

    /// <summary>
    /// A request record's non-nullable value type parameter without a default (an enum, number, bool or date) is
    /// required: when it is omitted the request is rejected instead of silently binding <c>default</c>. Strings stay
    /// optional for the serializer, so their validators report a missing value together with every other field error.
    /// </summary>
    private static void RequireValueTypeParameters(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
        {
            return;
        }

        foreach (JsonPropertyInfo property in typeInfo.Properties)
        {
            if (property.AssociatedParameter is { HasDefaultValue: false }
                && property.PropertyType.IsValueType
                && Nullable.GetUnderlyingType(property.PropertyType) is null)
            {
                property.IsRequired = true;
            }
        }
    }
}
