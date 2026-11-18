using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Host;

/// <summary>
/// Section 3: binding failures (unparseable JSON, wrong types, unknown enum values, omitted required values, bad
/// query values) are 400 <c>Validation</c> problems like any other; an unparseable or missing body is keyed <c>$</c>.
/// </summary>
public sealed class RequestBindingTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Theory]
    [InlineData("""{ "firstName": "Mia", """)]
    [InlineData("not json")]
    [InlineData("")]
    public async Task PutMe_WithUnparseableOrEmptyBody_Returns400KeyedByTheRoot(string body)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(HttpMethod.Put, IdentityRoutes.Me, body);

        JsonElement problem = await response.ShouldBeCompleteProblemAsync(HttpStatusCode.BadRequest, "Validation");
        problem.GetProperty("errors").EnumerateObject().Select(member => member.Name).ShouldBe(["$"]);
    }

    [Fact]
    public async Task PutMe_WithNullUnits_Returns400UnitsError()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(
            HttpMethod.Put,
            IdentityRoutes.Me,
            """{ "firstName": "Mia", "lastName": "Novak", "units": null, "timeZone": "UTC" }""");

        await response.ShouldBeValidationProblemAsync("units");
    }

    [Fact]
    public async Task PutMe_WithUnknownUnits_ListsTheAllowedValues()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(
            HttpMethod.Put,
            IdentityRoutes.Me,
            """{ "firstName": "Mia", "lastName": "Novak", "units": "stone", "timeZone": "UTC" }""");

        Problem problem = await response.ShouldBeValidationProblemAsync("units");
        problem.Errors!["units"].ShouldBe(["Must be one of: kg, lb."]);
    }

    [Fact]
    public async Task PutMe_WithWrongTypes_NeverEchoesServerTypeNames()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(
            HttpMethod.Put,
            IdentityRoutes.Me,
            """{ "firstName": ["Mia"], "lastName": "Novak", "units": "kg", "timeZone": "UTC" }""");

        await response.ShouldBeValidationProblemAsync("firstName");
        string body = await response.Content.ReadAsStringAsync(Ct);
        body.ShouldNotContain("TrainerSphere.");
        body.ShouldNotContain("System.");
    }

    [Fact]
    public async Task PutMe_WithANonJsonContentType_Returns415Problem()
    {
        TestUser trainer = await CreateTrainerAsync();
        using StringContent content = new("firstName=Mia");
        content.Headers.ContentType = new MediaTypeHeaderValue("text/plain");

        HttpResponseMessage response = await trainer.Client.PutAsync(IdentityRoutes.Me, content, Ct);

        await response.ShouldBeCompleteProblemAsync(HttpStatusCode.UnsupportedMediaType, "UnsupportedMediaType");
    }

    [Theory]
    [InlineData("?pageSize=ten", "pageSize")]
    [InlineData("?page=1.5", "page")]
    public async Task ListInvitations_WithNonIntegerPaging_Returns400ForThatParameter(string query, string field)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Invitations + query, Ct);

        await response.ShouldBeValidationProblemAsync(field);
    }

    [Fact]
    public async Task OpenApi_MarksOmittableValueTypesAsRequired()
    {
        JsonElement document = await (await CreateAnonymousClient().GetAsync(OpenApiTests.Document, Ct)).ReadJsonAsync();
        string reference = document.Property("paths./api/auth/me.put.requestBody.content.application/json.schema").GetProperty("$ref").GetString()!;
        JsonElement schema = document.GetProperty("components").GetProperty("schemas").GetProperty(reference["#/components/schemas/".Length..]);

        schema.GetProperty("required").EnumerateArray().Select(name => name.GetString()).ShouldContain("units");
    }
}
