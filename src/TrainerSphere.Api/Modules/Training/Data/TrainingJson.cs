using System.Text.Json;
using TrainerSphere.Api.Host;

namespace TrainerSphere.Api.Modules.Training.Data;

/// <summary>
/// Serializes snapshots stored as jsonb in the wire shape (camelCase, kebab-case enums), so they can be returned as is.
/// </summary>
internal static class TrainingJson
{
    private static readonly JsonSerializerOptions Options = Create();

    public static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Options);

    public static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, Options) ?? throw new InvalidOperationException($"Stored JSON for {typeof(T).Name} is null.");

    private static JsonSerializerOptions Create()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);
        return options;
    }
}
