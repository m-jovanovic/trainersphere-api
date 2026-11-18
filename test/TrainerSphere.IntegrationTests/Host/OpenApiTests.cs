using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Section 15: the OpenAPI document at <c>/openapi/v1.json</c> (which the web client can generate types from) and Scalar at <c>/scalar</c>.
/// </summary>
public sealed class OpenApiTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    public const string Document = "/openapi/v1.json";

    [Fact]
    public async Task OpenApiDocument_Anonymous_IsServedAsJson()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(Document, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        JsonElement document = await response.ReadJsonAsync();
        document.GetProperty("openapi").GetString().ShouldNotBeNull().ShouldStartWith("3.");
        document.Property("info.title").GetString().ShouldBe("TrainerSphere API");
    }

    [Fact]
    public async Task OpenApiDocument_DeclaresBearerJwtSecurity()
    {
        JsonElement document = await GetDocumentAsync();

        JsonElement bearer = document.Property("components.securitySchemes.Bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        bearer.GetProperty("bearerFormat").GetString().ShouldBe("JWT");
        document.GetProperty("security").EnumerateArray().Any(requirement => requirement.TryGetProperty("Bearer", out _)).ShouldBeTrue("No global Bearer requirement.");
    }

    [Theory]
    [InlineData("post", IdentityRoutes.Signup)]
    [InlineData("post", IdentityRoutes.Login)]
    [InlineData("post", IdentityRoutes.Refresh)]
    [InlineData("post", IdentityRoutes.Logout)]
    [InlineData("get", IdentityRoutes.Me)]
    [InlineData("put", IdentityRoutes.Me)]
    [InlineData("post", IdentityRoutes.ChangePassword)]
    [InlineData("post", IdentityRoutes.ForgotPassword)]
    [InlineData("post", IdentityRoutes.ResetPassword)]
    [InlineData("get", IdentityRoutes.Invitations)]
    [InlineData("post", IdentityRoutes.Invitations)]
    [InlineData("post", "/api/invitations/{id}/resend")]
    [InlineData("delete", "/api/invitations/{id}")]
    [InlineData("get", "/api/invitations/{token}")]
    [InlineData("post", "/api/invitations/{token}/accept")]
    public async Task OpenApiDocument_DocumentsContractRoute(string method, string path)
    {
        JsonElement document = await GetDocumentAsync();

        document.GetProperty("paths").TryGetProperty(path, out JsonElement item).ShouldBeTrue($"'{path}' is not documented.");
        item.TryGetProperty(method, out _).ShouldBeTrue($"'{method.ToUpperInvariant()} {path}' is not documented.");
    }

    [Fact]
    public async Task OpenApiDocument_DescribesUnitsWithTheContractEnumValues()
    {
        JsonElement document = await GetDocumentAsync();

        JsonElement response = Resolve(document, document.Property("paths./api/auth/me.get.responses.200.content.application/json.schema"));
        JsonElement request = Resolve(document, document.Property("paths./api/auth/me.put.requestBody.content.application/json.schema"));

        EnumValues(document, response.Property("properties.units")).ShouldBe(["kg", "lb"], ignoreOrder: true);
        EnumValues(document, request.Property("properties.units")).ShouldBe(["kg", "lb"], ignoreOrder: true);
    }

    [Fact]
    public async Task Scalar_Anonymous_ServesTheApiReference()
    {
        HttpClient client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = true });

        HttpResponseMessage response = await client.GetAsync("/scalar", Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
    }

    private static JsonElement Resolve(JsonElement document, JsonElement schema)
    {
        while (schema.TryGetProperty("$ref", out JsonElement reference))
        {
            string name = reference.GetString().ShouldNotBeNull()["#/components/schemas/".Length..];
            schema = document.GetProperty("components").GetProperty("schemas").GetProperty(name);
        }

        return schema;
    }

    private static string?[] EnumValues(JsonElement document, JsonElement property)
    {
        JsonElement schema = Resolve(document, property);
        schema.TryGetProperty("enum", out JsonElement values).ShouldBeTrue($"Not an enum schema: {schema}");
        return [.. values.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.String).Select(v => v.GetString())];
    }

    private async Task<JsonElement> GetDocumentAsync() =>
        await (await (await CreateAnonymousClient().GetAsync(Document, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK)).ReadJsonAsync();
}

/// <summary>
/// The host as it runs in production, where <c>OpenApi:Enabled</c> is off.
/// </summary>
public sealed class OpenApiDisabledApiFactory(PostgresServer postgres) : ApiFactory(postgres)
{
    protected override void ConfigureSettings(IDictionary<string, string?> settings) => settings["OpenApi:Enabled"] = "false";
}

public sealed class OpenApiDisabledTests(OpenApiDisabledApiFactory factory) : IntegrationTest(factory), IClassFixture<OpenApiDisabledApiFactory>
{
    [Theory]
    [InlineData(OpenApiTests.Document)]
    [InlineData("/scalar")]
    public async Task Docs_WhenDisabled_Return404(string route)
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(route, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NotFound);
    }
}
