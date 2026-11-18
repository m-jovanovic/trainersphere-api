using System.Net;
using System.Text.Json;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class InvitationTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string Domain = "@test.trainersphere.dev";

    public static TheoryData<string> InvalidNames => [string.Empty, "   ", new string('n', 101)];

    [Fact]
    public async Task CreateInvitation_WithValidRequest_Returns201Invitation()
    {
        TestUser trainer = await CreateTrainerAsync();
        CreateInvitationRequest request = new InvitationBuilder().WithMessage("Excited to work with you!").Build();
        DateTimeOffset before = DateTimeOffset.UtcNow.AddSeconds(-5);

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, request);

        Invitation invitation = await response.ShouldHaveJsonAsync<Invitation>(HttpStatusCode.Created);
        invitation.Id.ShouldNotBe(Guid.Empty);
        invitation.Email.ShouldBe(request.Email);
        invitation.FirstName.ShouldBe(request.FirstName);
        invitation.LastName.ShouldBe(request.LastName);
        invitation.Message.ShouldBe("Excited to work with you!");
        invitation.Status.ShouldBe("pending");
        invitation.CreatedAt.ShouldBeInRange(before, DateTimeOffset.UtcNow.AddSeconds(5));
        invitation.ExpiresAt.ShouldBe(invitation.CreatedAt.AddDays(7));
        invitation.AcceptedAt.ShouldBeNull();
        invitation.ClientId.ShouldBeNull();
    }

    [Fact]
    public async Task CreateInvitation_WithValidRequest_ReturnsEveryContractPropertyWithUtcTimestamps()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithMessage(null).Build());

        await response.ShouldHaveStatusAsync(HttpStatusCode.Created);
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.InvitationProperties);
        body.ShouldBeNullProperty("message");
        body.ShouldBeNullProperty("acceptedAt");
        body.ShouldBeNullProperty("clientId");
        body.ShouldBeUtcTimestamp("createdAt");
        body.ShouldBeUtcTimestamp("expiresAt");
    }

    [Fact]
    public async Task CreateInvitation_WithValidRequest_PublishesInvitationIssuedWithLinkToken()
    {
        TestUser trainer = await CreateTrainerAsync();

        Invitation invitation = await (await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithMessage("Hi!").Build()))
            .ShouldHaveJsonAsync<Invitation>(HttpStatusCode.Created);

        InvitationIssued issued = Factory.Events.OfType<InvitationIssued>().Where(e => e.InvitationId == invitation.Id).ShouldHaveSingleItem();
        issued.Email.ShouldBe(invitation.Email);
        issued.FirstName.ShouldBe(invitation.FirstName);
        issued.TrainerName.ShouldBe($"{trainer.FirstName} {trainer.LastName}");
        issued.Message.ShouldBe("Hi!");
        issued.Token.Length.ShouldBe(43);
        issued.Token.ShouldMatch("^[A-Za-z0-9_-]+$");
        Guid.TryParse(issued.Token, out _).ShouldBeFalse();
        new DateTimeOffset(issued.ExpiresAt, TimeSpan.Zero).ShouldBe(invitation.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task CreateInvitation_WithPaddedMixedCaseEmail_StoresTrimmedLowerCaseEmail()
    {
        TestUser trainer = await CreateTrainerAsync();
        string email = Unique.Email("client");

        Invitation invitation = (await InviteAsync(trainer, i => i.WithEmail($"  {email.ToUpperInvariant()} "))).Invitation;

        invitation.Email.ShouldBe(email);
    }

    [Fact]
    public async Task CreateInvitation_WithPaddedNamesAndMessage_TrimsThem()
    {
        TestUser trainer = await CreateTrainerAsync();

        Invitation invitation = (await InviteAsync(trainer, i => i.WithName("  Alex ", " Rivera  ").WithMessage("  See you Monday!  "))).Invitation;

        invitation.FirstName.ShouldBe("Alex");
        invitation.LastName.ShouldBe("Rivera");
        invitation.Message.ShouldBe("See you Monday!");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("    ")]
    public async Task CreateInvitation_WithoutMessage_StoresNullMessage(string? message)
    {
        TestUser trainer = await CreateTrainerAsync();

        Invitation invitation = (await InviteAsync(trainer, i => i.WithMessage(message))).Invitation;

        invitation.Message.ShouldBeNull();
    }

    [Fact]
    public async Task CreateInvitation_WithMessageOf500Characters_Returns201()
    {
        TestUser trainer = await CreateTrainerAsync();

        Invitation invitation = (await InviteAsync(trainer, i => i.WithMessage(new string('m', 500)))).Invitation;

        invitation.Message.ShouldNotBeNull().Length.ShouldBe(500);
    }

    [Fact]
    public async Task CreateInvitation_WithPendingInvitationForEmail_Returns409PendingExists()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation first = await InviteAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(first.Invitation.Email.ToUpperInvariant()).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.PendingExists");
        Factory.Events.OfType<InvitationIssued>().Count(e => e.Email == first.Invitation.Email).ShouldBe(1);
    }

    [Fact]
    public async Task CreateInvitation_AfterPreviousExpired_Returns201()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation first = await InviteAsync(trainer);
        await Factory.ExpireInvitationAsync(first.Invitation.Id);

        IssuedInvitation second = await InviteAsync(trainer, i => i.WithEmail(first.Invitation.Email));

        second.Invitation.Id.ShouldNotBe(first.Invitation.Id);
        second.Invitation.Status.ShouldBe("pending");
    }

    [Fact]
    public async Task CreateInvitation_AfterPreviousCancelled_Returns201()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation first = await InviteAsync(trainer);
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(first.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        IssuedInvitation second = await InviteAsync(trainer, i => i.WithEmail(first.Invitation.Email));

        second.Invitation.Status.ShouldBe("pending");
    }

    [Fact]
    public async Task CreateInvitation_ForTrainersOwnEmail_Returns409EmailInUse()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(trainer.Email).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Fact]
    public async Task CreateInvitation_ForAnotherTrainersEmail_Returns409EmailInUse()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser other = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(other.Email).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Fact]
    public async Task CreateInvitation_ForExistingClientEmail_Returns409EmailInUse()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(clients[0].Email).Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "User.EmailInUse");
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    [InlineData("alex@")]
    public async Task CreateInvitation_WithInvalidEmail_Returns400EmailError(string email)
    {
        TestUser trainer = await CreateTrainerAsync();

        await ShouldOnlyFailOn(await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(email).Build()), "email");
    }

    [Fact]
    public async Task CreateInvitation_WithEmailOver254Characters_Returns400EmailError()
    {
        TestUser trainer = await CreateTrainerAsync();
        string email = new string('a', 255 - Domain.Length) + Domain;

        await ShouldOnlyFailOn(await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(email).Build()), "email");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task CreateInvitation_WithInvalidFirstName_Returns400FirstNameError(string firstName)
    {
        TestUser trainer = await CreateTrainerAsync();

        await ShouldOnlyFailOn(await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithName(firstName, "Rivera").Build()), "firstName");
    }

    [Theory]
    [MemberData(nameof(InvalidNames))]
    public async Task CreateInvitation_WithInvalidLastName_Returns400LastNameError(string lastName)
    {
        TestUser trainer = await CreateTrainerAsync();

        await ShouldOnlyFailOn(await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithName("Alex", lastName).Build()), "lastName");
    }

    [Fact]
    public async Task CreateInvitation_WithMessageOver500Characters_Returns400MessageError()
    {
        TestUser trainer = await CreateTrainerAsync();

        await ShouldOnlyFailOn(await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithMessage(new string('m', 501)).Build()), "message");
    }

    [Fact]
    public async Task CreateInvitation_WithInvalidRequest_PublishesNothing()
    {
        TestUser trainer = await CreateTrainerAsync();
        string email = Unique.Email("client");

        await trainer.Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().WithEmail(email).WithMessage(new string('m', 501)).Build());

        Factory.Events.OfType<InvitationIssued>().ShouldNotContain(e => e.Email == email);
        (await trainer.ListInvitationsAsync()).Items.ShouldNotContain(i => i.Email == email);
    }

    [Fact]
    public async Task CreateInvitation_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task CreateInvitation_AsClient_Returns403()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await clients[0].Client.PostJsonAsync(IdentityRoutes.Invitations, new InvitationBuilder().Build());

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    [Fact]
    public async Task ListInvitations_ReturnsOwnInvitationsNewestFirst()
    {
        TestUser trainer = await CreateTrainerAsync();
        Guid[] created = [(await InviteAsync(trainer)).Invitation.Id, (await InviteAsync(trainer)).Invitation.Id, (await InviteAsync(trainer)).Invitation.Id];

        Paged<Invitation> page = await trainer.ListInvitationsAsync();

        page.Items.Select(i => i.Id).ShouldBe(created.Reverse());
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(20);
        page.TotalCount.ShouldBe(3);
        page.TotalPages.ShouldBe(1);
    }

    [Fact]
    public async Task ListInvitations_ReturnsEveryContractProperty()
    {
        TestUser trainer = await CreateTrainerAsync();
        await InviteAsync(trainer);

        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Invitations, Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.PagedProperties);
        body.GetProperty("items")[0].ShouldHaveExactProperties(IdentityRoutes.InvitationProperties);
    }

    [Fact]
    public async Task ListInvitations_ReturnsTheSameInvitationAsCreate()
    {
        TestUser trainer = await CreateTrainerAsync();
        Invitation created = (await InviteAsync(trainer)).Invitation;

        Invitation listed = (await trainer.ListInvitationsAsync()).Items.ShouldHaveSingleItem();

        listed.ShouldBe(created with { CreatedAt = listed.CreatedAt, ExpiresAt = listed.ExpiresAt });
        listed.CreatedAt.ShouldBe(created.CreatedAt, TimeSpan.FromMilliseconds(1));
        listed.ExpiresAt.ShouldBe(created.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ListInvitations_ForNewTrainer_ReturnsEmptyPage()
    {
        TestUser trainer = await CreateTrainerAsync();

        Paged<Invitation> page = await trainer.ListInvitationsAsync();

        page.Items.ShouldBeEmpty();
        page.TotalCount.ShouldBe(0);
        page.TotalPages.ShouldBe(0);
    }

    [Fact]
    public async Task ListInvitations_DoesNotIncludeOtherTrainersInvitations()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser other = await CreateTrainerAsync();
        Invitation mine = (await InviteAsync(trainer)).Invitation;
        Invitation theirs = (await InviteAsync(other)).Invitation;

        Paged<Invitation> page = await trainer.ListInvitationsAsync();

        page.Items.Select(i => i.Id).ShouldBe([mine.Id]);
        page.Items.ShouldNotContain(i => i.Id == theirs.Id);
    }

    [Fact]
    public async Task ListInvitations_ReportsEveryStatus()
    {
        TestUser trainer = await CreateTrainerAsync();
        (Guid pending, Guid accepted, Guid cancelled, Guid expired) = await CreateOneOfEachStatusAsync(trainer);

        var statuses = (await trainer.ListInvitationsAsync()).Items.ToDictionary(i => i.Id, i => i.Status);

        statuses[pending].ShouldBe("pending");
        statuses[accepted].ShouldBe("accepted");
        statuses[cancelled].ShouldBe("cancelled");
        statuses[expired].ShouldBe("expired");
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("accepted")]
    [InlineData("cancelled")]
    [InlineData("expired")]
    public async Task ListInvitations_WithStatusFilter_ReturnsOnlyThatStatus(string status)
    {
        TestUser trainer = await CreateTrainerAsync();
        (Guid pending, Guid accepted, Guid cancelled, Guid expired) = await CreateOneOfEachStatusAsync(trainer);
        Guid expected = status switch
        {
            "pending" => pending,
            "accepted" => accepted,
            "cancelled" => cancelled,
            _ => expired,
        };

        Paged<Invitation> page = await trainer.ListInvitationsAsync($"?status={status}");

        page.Items.Select(i => i.Id).ShouldBe([expected]);
        page.Items[0].Status.ShouldBe(status);
        page.TotalCount.ShouldBe(1);
    }

    [Fact]
    public async Task ListInvitations_AcceptedInvitation_HasAcceptedAtAndClientId()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser client = await CreateClientAsync(trainer);

        Invitation invitation = (await trainer.ListInvitationsAsync("?status=accepted")).Items.ShouldHaveSingleItem();

        invitation.Email.ShouldBe(client.Email);
        invitation.ClientId.ShouldBe(client.Id);
        invitation.AcceptedAt.ShouldNotBeNull().ShouldBeInRange(invitation.CreatedAt, DateTimeOffset.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task ListInvitations_WithPaging_ReturnsRequestedSlice()
    {
        TestUser trainer = await CreateTrainerAsync();
        Guid[] created = [(await InviteAsync(trainer)).Invitation.Id, (await InviteAsync(trainer)).Invitation.Id, (await InviteAsync(trainer)).Invitation.Id];

        Paged<Invitation> first = await trainer.ListInvitationsAsync("?page=1&pageSize=2");
        Paged<Invitation> second = await trainer.ListInvitationsAsync("?page=2&pageSize=2");
        Paged<Invitation> beyond = await trainer.ListInvitationsAsync("?page=3&pageSize=2");

        first.Items.Select(i => i.Id).ShouldBe([created[2], created[1]]);
        second.Items.Select(i => i.Id).ShouldBe([created[0]]);
        beyond.Items.ShouldBeEmpty();
        foreach (Paged<Invitation> page in new[] { first, second, beyond })
        {
            page.PageSize.ShouldBe(2);
            page.TotalCount.ShouldBe(3);
            page.TotalPages.ShouldBe(2);
        }

        second.Page.ShouldBe(2);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(100)]
    public async Task ListInvitations_WithPageSizeAtLimit_Returns200(int pageSize)
    {
        TestUser trainer = await CreateTrainerAsync();

        (await trainer.ListInvitationsAsync($"?pageSize={pageSize}")).PageSize.ShouldBe(pageSize);
    }

    [Theory]
    [InlineData("?status=bogus", "status")]
    [InlineData("?status=Pending", "status")]
    [InlineData("?page=0", "page")]
    [InlineData("?page=-1", "page")]
    [InlineData("?pageSize=0", "pageSize")]
    [InlineData("?pageSize=101", "pageSize")]
    public async Task ListInvitations_WithInvalidQuery_Returns400ForThatParameter(string query, string field)
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Invitations + query, Ct);

        await ShouldOnlyFailOn(response, field);
    }

    [Fact]
    public async Task ListInvitations_WithNonNumericPage_Returns400Validation()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Invitations + "?page=abc", Ct);

        await response.ShouldBeValidationProblemAsync("page");
    }

    [Fact]
    public async Task ListInvitations_WithoutToken_Returns401()
    {
        HttpResponseMessage response = await CreateAnonymousClient().GetAsync(IdentityRoutes.Invitations, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task ListInvitations_AsClient_Returns403()
    {
        (_, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();

        HttpResponseMessage response = await clients[0].Client.GetAsync(IdentityRoutes.Invitations, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }

    private static async Task ShouldOnlyFailOn(HttpResponseMessage response, string field)
    {
        Problem problem = await response.ShouldBeValidationProblemAsync(field);
        problem.Errors.ShouldNotBeNull().Keys.ShouldBe([field]);
    }

    private async Task<(Guid Pending, Guid Accepted, Guid Cancelled, Guid Expired)> CreateOneOfEachStatusAsync(TestUser trainer)
    {
        Guid pending = (await InviteAsync(trainer)).Invitation.Id;

        IssuedInvitation accepted = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(accepted.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        Guid cancelled = (await InviteAsync(trainer)).Invitation.Id;
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(cancelled), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        Guid expired = (await InviteAsync(trainer)).Invitation.Id;
        await Factory.ExpireInvitationAsync(expired);

        await Factory.WaitForMessagesAsync();
        return (pending, accepted.Invitation.Id, cancelled, expired);
    }
}
