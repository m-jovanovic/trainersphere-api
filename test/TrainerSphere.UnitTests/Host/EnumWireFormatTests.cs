using System.Text.Json;
using System.Text.RegularExpressions;
using Shouldly;
using TrainerSphere.Api.Host;
using TrainerSphere.SharedKernel.Http;

namespace TrainerSphere.UnitTests.Host;

/// <summary>
/// QA-F10: every enum that can reach the wire serializes to the contract's kebab-case form and parses back to the
/// same value. Digits keep their own segment (<c>percent-1rm</c>, not <c>percent1-rm</c>), which a plain naming
/// policy gets wrong.
/// </summary>
public sealed class EnumWireFormatTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();
    private static readonly Regex WireForm = new("^[a-z]+(-[a-z0-9]+)*$", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

    public static TheoryData<Type> EnumTypes =>
    [
        .. typeof(DemoAccounts).Assembly.GetTypes().Where(t => t.IsEnum),
        .. typeof(KebabCase).Assembly.GetTypes().Where(t => t.IsEnum),
    ];

    [Theory]
    [MemberData(nameof(EnumTypes))]
    public void EveryEnumValue_SerializesInContractKebabForm_AndParsesBack(Type enumType)
    {
        foreach (object value in Enum.GetValues(enumType))
        {
            string json = JsonSerializer.Serialize(value, value.GetType(), Options);
            string wire = JsonSerializer.Deserialize<string>(json)!;

            WireForm.IsMatch(wire).ShouldBeTrue($"{enumType.Name}.{value} serializes as '{wire}'; digits need their own segment, for example percent-1rm.");
            JsonSerializer.Deserialize(json, value.GetType(), Options).ShouldBe(value);
        }
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
