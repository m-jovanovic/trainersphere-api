using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using TrainerSphere.SharedKernel.Http;

namespace TrainerSphere.Api.Host;

/// <summary>
/// Describes enums as their kebab-case strings; the schema generator cannot see through the custom enum converter.
/// </summary>
internal sealed class KebabCaseEnumSchemaTransformer : IOpenApiSchemaTransformer
{
    public Task TransformAsync(OpenApiSchema schema, OpenApiSchemaTransformerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(context);

        Type type = context.JsonTypeInfo.Type;
        Type? underlying = Nullable.GetUnderlyingType(type);
        Type enumType = underlying ?? type;

        if (!enumType.IsEnum)
        {
            return Task.CompletedTask;
        }

        schema.Type = underlying is null ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;
        schema.Format = null;
        schema.Enum = [.. Enum.GetValues(enumType).Cast<Enum>().Select(value => (System.Text.Json.Nodes.JsonNode)value.ToKebab())];

        return Task.CompletedTask;
    }
}
