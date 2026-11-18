using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Shouldly;
using TrainerSphere.Api.Host;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.Host;

public sealed class JsonBindingTests
{
    private static readonly JsonSerializerOptions Options = CreateOptions();

    public enum Stage
    {
        NotStarted,
        InProgress,
    }

    [Theory]
    [InlineData("\"not-started\"", Stage.NotStarted)]
    [InlineData("\"in-progress\"", Stage.InProgress)]
    public void Enum_ExactKebabCase_Reads(string json, Stage expected) =>
        JsonSerializer.Deserialize<Stage>(json, Options).ShouldBe(expected);

    [Theory]
    [InlineData("\"In-Progress\"")]
    [InlineData("\"IN-PROGRESS\"")]
    [InlineData("\"InProgress\"")]
    [InlineData("\"inprogress\"")]
    [InlineData("\"\"")]
    [InlineData("1")]
    [InlineData("true")]
    public void Enum_AnythingButExactKebabCase_IsAFieldErrorListingTheValues(string value)
    {
        ValidationError error = BindingErrorFor($$"""{ "name": "a", "stage": {{value}}, "count": 1 }""");

        FieldError field = error.Errors.ShouldHaveSingleItem();
        field.Field.ShouldBe("stage");
        field.Message.ShouldBe("Must be one of: not-started, in-progress.");
    }

    [Fact]
    public void Enum_Writes_KebabCase() =>
        JsonSerializer.Serialize(Stage.InProgress, Options).ShouldBe("\"in-progress\"");

    [Fact]
    public void Enum_AsDictionaryKey_RoundTrips()
    {
        Dictionary<Stage, int> counts = new() { [Stage.InProgress] = 2 };

        string json = JsonSerializer.Serialize(counts, Options);

        json.ShouldBe("""{"in-progress":2}""");
        JsonSerializer.Deserialize<Dictionary<Stage, int>>(json, Options).ShouldBe(counts);
    }

    [Fact]
    public void NullableEnum_AcceptsNull() =>
        JsonSerializer.Deserialize<Item>("""{ "name": "a", "stage": "in-progress", "count": 1, "previous": null }""", Options)!.Previous.ShouldBeNull();

    [Fact]
    public void OmittedValueTypes_AreRequired_AndReportedTogether()
    {
        ValidationError error = BindingErrorFor("""{ "name": "a" }""");

        error.Errors.Select(e => e.Field).ShouldBe(["stage", "count"], ignoreOrder: true);
        error.Errors.ShouldAllBe(e => e.Message == BindingErrors.Required && e.Code == ValidationError.ValidationCode);
    }

    [Fact]
    public void OmittedOptionalMembers_BindTheirDefaults()
    {
        Item item = JsonSerializer.Deserialize<Item>("""{ "stage": "not-started", "count": 3 }""", Options)!;

        item.Name.ShouldBeNull();
        item.Previous.ShouldBeNull();
        item.Archived.ShouldBeFalse();
    }

    [Fact]
    public void OmittedValueType_InANestedObject_IsKeyedByItsPath()
    {
        ValidationError error = BindingErrorFor<Batch>("""{ "items": [{ "name": "a", "stage": "in-progress", "count": 1 }, { "name": "b", "stage": "in-progress" }] }""");

        error.Errors.ShouldHaveSingleItem().Field.ShouldBe("items[1].count");
    }

    [Theory]
    [InlineData("""{ "name": 42, "stage": "in-progress", "count": 1 }""", "name")]
    [InlineData("""{ "name": "a", "stage": "in-progress", "count": "many" }""", "count")]
    [InlineData("""{ "name": "a", "stage": "in-progress", "count": null }""", "count")]
    [InlineData("""{ "name": "a", "stage": "in-progress", "count": 1.5 }""", "count")]
    public void WrongJsonType_IsKeyedByTheField(string json, string field)
    {
        FieldError error = BindingErrorFor(json).Errors.ShouldHaveSingleItem();

        error.Field.ShouldBe(field);
        error.Message.ShouldBe(BindingErrors.InvalidValue);
    }

    [Fact]
    public void WrongJsonType_InAnArray_IsKeyedByItsIndexedPath() =>
        BindingErrorFor<Batch>("""{ "items": [{ "name": "a", "stage": "in-progress", "count": "x" }] }""").Errors.ShouldHaveSingleItem().Field.ShouldBe("items[0].count");

    [Theory]
    [InlineData("""{ "name": "a", """)]
    [InlineData("""{ name: "a" }""")]
    [InlineData("not json")]
    [InlineData("""{ "name": "a", "stage": "in-progress", "count": 1 } trailing""")]
    public void UnparseableJson_IsKeyedByTheRoot(string json)
    {
        FieldError error = BindingErrorFor(json).Errors.ShouldHaveSingleItem();

        error.Field.ShouldBe(BindingErrors.BodyField);
        error.Message.ShouldBe(BindingErrors.InvalidJson);
    }

    [Theory]
    [InlineData("Failed to bind parameter \"int? page\" from \"abc\".", "page", BindingErrors.InvalidValue)]
    [InlineData("Failed to bind parameter \"Guid id\" from \"x\".", "id", BindingErrors.InvalidValue)]
    [InlineData("Required parameter \"int pageSize\" was not provided from query string.", "pageSize", BindingErrors.Required)]
    public void ParameterFailures_AreKeyedByTheParameterName(string message, string field, string text)
    {
        FieldError error = BindingErrors.From(new BadHttpRequestException(message)).Errors.ShouldHaveSingleItem();

        error.Field.ShouldBe(field);
        error.Message.ShouldBe(text);
    }

    [Theory]
    [InlineData("Required parameter \"UpdateMeRequest request\" was not provided from body.")]
    [InlineData("Implicit body inferred for parameter \"request\" but no body was provided. Did you mean to use a Service instead?")]
    public void MissingBody_IsKeyedByTheRoot(string message)
    {
        FieldError error = BindingErrors.From(new BadHttpRequestException(message)).Errors.ShouldHaveSingleItem();

        error.Field.ShouldBe(BindingErrors.BodyField);
        error.Message.ShouldBe(BindingErrors.MissingBody);
    }

    [Fact]
    public void FieldErrors_NeverEchoTypeNames()
    {
        ValidationError error = BindingErrorFor("""{ "name": "a", "stage": "in-progress", "count": "x" }""");

        error.Errors.ShouldAllBe(e => !e.Message.Contains(nameof(Item), StringComparison.Ordinal) && !e.Message.Contains("System.", StringComparison.Ordinal));
    }

    private static ValidationError BindingErrorFor(string json) => BindingErrorFor<Item>(json);

    private static ValidationError BindingErrorFor<T>(string json)
    {
        JsonException exception = Should.Throw<JsonException>(() => JsonSerializer.Deserialize<T>(json, Options));
        return BindingErrors.From(new BadHttpRequestException("Failed to read parameter \"T request\" from the request body as JSON.", exception));
    }

    private static JsonSerializerOptions CreateOptions()
    {
        JsonSerializerOptions options = new(JsonSerializerDefaults.Web);
        JsonDefaults.Configure(options);
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }

    private sealed record Item(string Name, Stage Stage, int Count, Stage? Previous, bool Archived = false);

    private sealed record Batch(IReadOnlyList<Item> Items);
}
