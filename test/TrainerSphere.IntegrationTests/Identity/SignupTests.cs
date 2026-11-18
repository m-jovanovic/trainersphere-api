using System.Net;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class SignupTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string Domain = "@test.trainersphere.dev";

    public static TheoryData<string> InvalidNames => [string.Empty, "   ", new string('n', 101)];

    [Fact]
    public async Task Signup_WithValidRequest_Returns201AuthResponse()
    {
        SignupRequest request = new SignupBuilder().Build();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-5);

        HttpResponseMessage response = await SignupAsync(request);

        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldBe(IdentityRoutes.Me);
        auth.TokenType.ShouldBe("Bearer");
        auth.ExpiresIn.ShouldBe(900);
        auth.AccessToken.ShouldNotBeNullOrWhiteSpace();
        auth.User.Id.ShouldNotBe(Guid.Empty);
        auth.User.Email.ShouldBe(request.Email);
        auth.User.FirstName.ShouldBe(request.FirstName);
        auth.User.LastName.ShouldBe(request.LastName);
        auth.User.Role.ShouldBe("trainer");
        auth.User.TrainerId.ShouldBeNull();
        auth.User.Units.ShouldBe("kg");
        auth.User.TimeZone.ShouldBe(request.TimeZone);
        auth.User.CreatedAt.ShouldBeInRange(before, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task Signup_WithValidRequest_ReturnsEveryContractPropertyWithUtcTimestamps()
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().Build());

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.AuthResponseProperties);
        JsonElement user = body.GetProperty("user").ShouldHaveExactProperties(IdentityRoutes.UserProperties);
        user.ShouldBeNullProperty("trainerId");
        user.ShouldBeUtcTimestamp("createdAt");
    }

    [Fact]
    public async Task Signup_WithValidRequest_SetsRefreshCookie()
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().Build());

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        response.ShouldSetRefreshCookie();
    }

    [Fact]
    public async Task Signup_WithValidRequest_IssuesAccessTokenWithContractClaims()
    {
        SignupRequest request = new SignupBuilder().Build();

        AuthResponse auth = await (await SignupAsync(request)).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        JsonWebToken token = AccessTokens.Read(auth.AccessToken);
        token.Alg.ShouldBe("HS256");
        token.Claim("sub").ShouldBe(auth.User.Id.ToString());
        token.Claim("email").ShouldBe(request.Email);
        token.Claim("role").ShouldBe("trainer");
        token.Claim("name").ShouldBe($"{request.FirstName} {request.LastName}");
        token.Claim("trainer_id").ShouldBeNull();
        (token.ValidTo - token.IssuedAt).ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task Signup_WithValidRequest_AccessTokenAuthorizesTheNewUser()
    {
        AuthResponse auth = await (await SignupAsync(new SignupBuilder().Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        User me = await (await Factory.CreateBearerClient(auth.AccessToken).GetAsync(IdentityRoutes.Me, Ct)).ShouldHaveJsonAsync<User>();

        me.ShouldBe(auth.User with { CreatedAt = me.CreatedAt });
        me.CreatedAt.ShouldBe(auth.User.CreatedAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Signup_CreatedAt_IsTheSameValueGetMeReturns()
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().Build());
        AuthResponse auth = await response.ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);
        string? signedUpAt = (await response.ReadJsonAsync()).Property("user.createdAt").GetString();

        HttpResponseMessage me = await Factory.CreateBearerClient(auth.AccessToken).GetAsync(IdentityRoutes.Me, Ct);

        (await me.ReadJsonAsync()).GetProperty("createdAt").GetString().ShouldBe(signedUpAt);
    }

    [Fact]
    public async Task Signup_WithValidRequest_PublishesTrainerRegistered()
    {
        SignupRequest request = new SignupBuilder().Build();

        AuthResponse auth = await (await SignupAsync(request)).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created);

        TrainerRegistered registered = Factory.Events.OfType<TrainerRegistered>().Where(e => e.UserId == auth.User.Id).ShouldHaveSingleItem();
        registered.Email.ShouldBe(request.Email);
        registered.FirstName.ShouldBe(request.FirstName);
        registered.LastName.ShouldBe(request.LastName);
        registered.TimeZone.ShouldBe(request.TimeZone);
    }

    [Fact]
    public async Task Signup_WithoutTimeZone_DefaultsToUtc()
    {
        User user = (await (await SignupAsync(new SignupBuilder().WithTimeZone(null).Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created)).User;

        user.TimeZone.ShouldBe("UTC");
    }

    [Theory]
    [InlineData("UTC")]
    [InlineData("America/New_York")]
    [InlineData("Asia/Kolkata")]
    public async Task Signup_WithIanaTimeZone_StoresIt(string timeZone)
    {
        User user = (await (await SignupAsync(new SignupBuilder().WithTimeZone(timeZone).Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created)).User;

        user.TimeZone.ShouldBe(timeZone);
    }

    [Fact]
    public async Task Signup_WithPaddedMixedCaseEmail_StoresTrimmedLowerCaseEmail()
    {
        string email = Unique.Email("mixed");

        User user = (await (await SignupAsync(new SignupBuilder().WithEmail($"  {email.ToUpperInvariant()} ").Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created)).User;

        user.Email.ShouldBe(email);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(email, "Trainer-pass-1"))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signup_WithPaddedNames_TrimsNames()
    {
        string lastName = Unique.Name("Novak");

        User user = (await (await SignupAsync(new SignupBuilder().WithName("  Maya ", $" {lastName}  ").Build())).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created)).User;

        user.FirstName.ShouldBe("Maya");
        user.LastName.ShouldBe(lastName);
    }

    [Fact]
    public async Task Signup_AtMaximumLengths_Returns201()
    {
        string email = Guid.NewGuid().ToString("N") + new string('a', 254 - 32 - Domain.Length) + Domain;
        SignupRequest request = new SignupBuilder()
            .WithEmail(email)
            .WithPassword(new string('p', 128))
            .WithName(new string('f', 100), new string('l', 100))
            .Build();

        User user = (await (await SignupAsync(request)).ShouldHaveJsonAsync<AuthResponse>(HttpStatusCode.Created)).User;

        user.Email.Length.ShouldBe(254);
        user.FirstName.Length.ShouldBe(100);
        user.LastName.Length.ShouldBe(100);
    }

    [Fact]
    public async Task Signup_AtMinimumLengths_Returns201()
    {
        SignupRequest request = new SignupBuilder().WithPassword("12345678").WithName("M", "N").Build();

        await (await SignupAsync(request)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(request.Email, "12345678"))).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Signup_WithTakenEmail_Returns409EmailInUse()
    {
        SignedIn existing = await Factory.SignupAsync();

        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithEmail(existing.User.Email).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
        response.ShouldNotSetRefreshCookie();
    }

    [Fact]
    public async Task Signup_WithTakenEmailInOtherCase_Returns409EmailInUse()
    {
        SignedIn existing = await Factory.SignupAsync();

        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithEmail($" {existing.User.Email.ToUpperInvariant()}").Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Fact]
    public async Task Signup_WithTakenEmail_KeepsExistingAccountAndPublishesNothing()
    {
        SignedIn existing = await Factory.SignupAsync();

        await SignupAsync(new SignupBuilder().WithEmail(existing.User.Email).WithPassword("Another-pass-9").Build());

        Factory.Events.OfType<TrainerRegistered>().Count(e => e.Email == existing.User.Email).ShouldBe(1);
        await Factory.LoginAsync(existing.User.Email, existing.Password);
        await (await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Login, new LoginRequest(existing.User.Email, "Another-pass-9")))
            .ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.InvalidCredentials");
    }

    [Fact]
    public async Task Signup_WithTakenClientEmail_Returns409EmailInUse()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithEmail(clients[0].Email).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    public async Task Signup_WithInvalidEmail_Returns400EmailError(string email)
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithEmail(email).Build());

        await ShouldOnlyFailOn(response, "email");
    }

    [Fact]
    public async Task Signup_WithEmailOver254Characters_Returns400EmailError()
    {
        string email = new string('a', 255 - Domain.Length) + Domain;

        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithEmail(email).Build());

        await ShouldOnlyFailOn(response, "email");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(129)]
    public async Task Signup_WithPasswordOfInvalidLength_Returns400PasswordError(int length)
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithPassword(new string('p', length)).Build());

        await ShouldOnlyFailOn(response, "password");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Signup_WithInvalidFirstName_Returns400FirstNameError(string firstName)
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithName(firstName, "Novak").Build());

        await ShouldOnlyFailOn(response, "firstName");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task Signup_WithInvalidLastName_Returns400LastNameError(string lastName)
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithName("Maya", lastName).Build());

        await ShouldOnlyFailOn(response, "lastName");
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not/AZone")]
    [InlineData("Mars/Olympus_Mons")]
    [InlineData("GMT+25")]
    public async Task Signup_WithInvalidTimeZone_Returns400TimeZoneError(string timeZone)
    {
        HttpResponseMessage response = await SignupAsync(new SignupBuilder().WithTimeZone(timeZone).Build());

        await ShouldOnlyFailOn(response, "timeZone");
    }

    [Fact]
    public async Task Signup_WithEmptyObject_Returns400ForEveryRequiredField()
    {
        HttpResponseMessage response = await CreateAnonymousClient().SendRawJsonAsync(HttpMethod.Post, IdentityRoutes.Signup, "{}");

        Problem problem = await response.ShouldBeValidationProblemAsync("email", "password", "firstName", "lastName");
        problem.Errors.ShouldNotBeNull().ShouldNotContainKey("timeZone");
    }

    [Fact]
    public async Task Signup_WithInvalidRequest_CreatesNoAccount()
    {
        SignupRequest request = new SignupBuilder().WithPassword("short").Build();

        await SignupAsync(request);

        (await Factory.QuerySingleAsync<int>("SELECT count(*)::int FROM identity.users WHERE email = @Email", new { request.Email })).ShouldBe(0);
        Factory.Events.OfType<TrainerRegistered>().ShouldNotContain(e => e.Email == request.Email);
    }

    [Fact]
    public async Task Signup_WithMalformedJson_Returns400Validation()
    {
        HttpResponseMessage response = await CreateAnonymousClient().SendRawJsonAsync(HttpMethod.Post, IdentityRoutes.Signup, """{ "email": "maya@example.com", """);

        await response.ShouldBeProblemAsync(HttpStatusCode.BadRequest, "Validation");
    }

    [Fact]
    public async Task Signup_WithWrongJsonType_Returns400ValidationForThatField()
    {
        HttpResponseMessage response = await CreateAnonymousClient().SendRawJsonAsync(
            HttpMethod.Post,
            IdentityRoutes.Signup,
            """{ "email": 42, "password": "Trainer-pass-1", "firstName": "Maya", "lastName": "Novak" }""");

        await response.ShouldBeValidationProblemAsync("email");
    }

    private static async Task ShouldOnlyFailOn(HttpResponseMessage response, string field)
    {
        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
        response.ShouldNotSetRefreshCookie();
    }

    private Task<HttpResponseMessage> SignupAsync(SignupRequest request) =>
        CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Signup, request);
}
