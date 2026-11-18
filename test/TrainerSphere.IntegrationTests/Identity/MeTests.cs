using System.Net;
using System.Text.Json;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class MeTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    public static TheoryData<string> InvalidNames => [string.Empty, "   ", new string('n', 101)];

    [Fact]
    public async Task GetMe_AsTrainer_Returns200User()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Me, Ct);

        User me = await response.ShouldHaveJsonAsync<User>();
        me.Id.ShouldBe(trainer.Id);
        me.Email.ShouldBe(trainer.Email);
        me.FirstName.ShouldBe(trainer.FirstName);
        me.LastName.ShouldBe(trainer.LastName);
        me.Role.ShouldBe("trainer");
        me.TrainerId.ShouldBeNull();
        me.Units.ShouldBe("kg");
        me.TimeZone.ShouldBe("Europe/Belgrade");
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.UserProperties);
        body.ShouldBeNullProperty("trainerId");
        body.ShouldBeUtcTimestamp("createdAt");
    }

    [Fact]
    public async Task GetMe_AsClient_Returns200ClientOfTheTrainer()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        User me = await (await clients[0].Client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();

        me.Id.ShouldBe(clients[0].Id);
        me.Role.ShouldBe("client");
        me.TrainerId.ShouldBe(trainer.Id);
    }

    [Fact]
    public async Task GetMe_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(IdentityRoutes.Me, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task PutMe_WithValidRequest_Returns200UpdatedUser()
    {
        TestUser trainer = await CreateTrainerAsync();
        User before = await (await trainer.Client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();

        HttpResponseMessage response = await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novakova", "lb", "America/New_York"));

        User updated = await response.ShouldHaveJsonAsync<User>();
        updated.ShouldBe(before with { FirstName = "Mia", LastName = "Novakova", Units = "lb", TimeZone = "America/New_York" });
        (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.UserProperties);
    }

    [Fact]
    public async Task PutMe_WithValidRequest_PersistsChanges()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novakova", "lb", "Asia/Tokyo"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        User me = await (await trainer.Client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();
        me.FirstName.ShouldBe("Mia");
        me.LastName.ShouldBe("Novakova");
        me.Units.ShouldBe("lb");
        me.TimeZone.ShouldBe("Asia/Tokyo");
        (await LoginAsync(trainer.Email, trainer.Password)).FirstName.ShouldBe("Mia");
    }

    [Fact]
    public async Task PutMe_BackToKg_Persists()
    {
        TestUser trainer = await CreateTrainerAsync();
        await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "lb", "UTC"));

        User me = await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "kg", "UTC"))).ShouldHaveJsonAsync<User>();

        me.Units.ShouldBe("kg");
    }

    [Fact]
    public async Task PutMe_WithPaddedNames_TrimsNames()
    {
        TestUser trainer = await CreateTrainerAsync();

        User me = await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("  Mia ", " Novak  ", "kg", "UTC"))).ShouldHaveJsonAsync<User>();

        me.FirstName.ShouldBe("Mia");
        me.LastName.ShouldBe("Novak");
    }

    [Fact]
    public async Task PutMe_WithValidRequest_PublishesUserProfileUpdated()
    {
        TestUser trainer = await CreateTrainerAsync();

        await (await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novakova", "lb", "Asia/Tokyo"))).ShouldHaveStatusAsync(HttpStatusCode.OK);

        UserProfileUpdated updated = Factory.Events.Last<UserProfileUpdated>(e => e.UserId == trainer.Id);
        updated.FirstName.ShouldBe("Mia");
        updated.LastName.ShouldBe("Novakova");
        updated.Units.ShouldBe("lb");
        updated.TimeZone.ShouldBe("Asia/Tokyo");
    }

    [Fact]
    public async Task PutMe_AsClient_UpdatesProfileAndKeepsTrainer()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        User me = await (await clients[0].Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Alexa", "Rivera", "lb", "Europe/London"))).ShouldHaveJsonAsync<User>();

        me.FirstName.ShouldBe("Alexa");
        me.Role.ShouldBe("client");
        me.TrainerId.ShouldBe(trainer.Id);
        me.Email.ShouldBe(clients[0].Email);
    }

    [Fact]
    public async Task PutMe_DoesNotChangeOtherUsers()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser other = await CreateTrainerAsync();

        await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Changed", "Name", "lb", "UTC"));

        User otherMe = await (await other.Client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();
        otherMe.FirstName.ShouldBe(other.FirstName);
        otherMe.Units.ShouldBe("kg");
    }

    [Fact]
    public async Task PutMe_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "kg", "UTC"));

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task PutMe_WithInvalidFirstName_Returns400FirstNameError(string firstName)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest(firstName, "Novak", "kg", "UTC"));

        await ShouldOnlyFailOn(response, "firstName");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task PutMe_WithInvalidLastName_Returns400LastNameError(string lastName)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", lastName, "kg", "UTC"));

        await ShouldOnlyFailOn(response, "lastName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not/AZone")]
    [InlineData("Mars/Olympus_Mons")]
    public async Task PutMe_WithInvalidTimeZone_Returns400TimeZoneError(string timeZone)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "kg", timeZone));

        await ShouldOnlyFailOn(response, "timeZone");
    }

    [Fact]
    public async Task PutMe_WithoutTimeZone_Returns400TimeZoneError()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(HttpMethod.Put, IdentityRoutes.Me, """{ "firstName": "Mia", "lastName": "Novak", "units": "kg" }""");

        await ShouldOnlyFailOn(response, "timeZone");
    }

    [Fact]
    public async Task PutMe_WithInvalidRequest_ChangesNothing()
    {
        TestUser trainer = await CreateTrainerAsync();

        await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "lb", "Not/AZone"));

        User me = await (await trainer.Client.GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();
        me.Units.ShouldBe("kg");
        me.FirstName.ShouldBe(trainer.FirstName);
        Factory.Events.OfType<UserProfileUpdated>().ShouldNotContain(e => e.UserId == trainer.Id);
    }

    [Theory]
    [InlineData("stone")]
    [InlineData("KG")]
    [InlineData("")]
    public async Task PutMe_WithUnknownUnits_Returns400UnitsError(string units)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", units, "UTC"));

        await response.ShouldBeValidationProblemAsync("units");
    }

    [Fact]
    public async Task PutMe_WithoutUnits_Returns400UnitsError()
    {
        TestUser trainer = await CreateTrainerAsync();
        await trainer.Client.PutJsonAsync(IdentityRoutes.Me, new UpdateMeRequest("Mia", "Novak", "lb", "UTC"));

        HttpResponseMessage response = await trainer.Client.SendRawJsonAsync(HttpMethod.Put, IdentityRoutes.Me, """{ "firstName": "Mia", "lastName": "Novak", "timeZone": "UTC" }""");

        await response.ShouldBeValidationProblemAsync("units");
    }

    private static async Task ShouldOnlyFailOn(HttpResponseMessage response, string field)
    {
        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
    }
}
