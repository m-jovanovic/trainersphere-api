using System.Net;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class InvitationResendTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task ResendInvitation_ExpiresAt_IsTheValueTheListReturns()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        System.Text.Json.JsonElement resent = await (await response.ShouldHaveStatusAsync(HttpStatusCode.OK)).ReadJsonAsync();
        System.Text.Json.JsonElement listed = (await (await trainer.Client.GetAsync(IdentityRoutes.Invitations, Ct)).ReadJsonAsync())
            .GetProperty("items").EnumerateArray().Single(i => i.GetProperty("id").GetString() == issued.Invitation.Id.ToString());
        listed.GetProperty("expiresAt").GetString().ShouldBe(resent.GetProperty("expiresAt").GetString());
    }

    [Fact]
    public async Task ResendInvitation_WhenPending_Returns200WithRestartedExpiry()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.ExecuteAsync("UPDATE identity.invitations SET expires_at = now() + interval '1 day' WHERE id = @Id", new { issued.Invitation.Id });
        DateTimeOffset before = DateTimeOffset.UtcNow;

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        Invitation resent = await response.ShouldHaveJsonAsync<Invitation>();
        resent.Id.ShouldBe(issued.Invitation.Id);
        resent.Email.ShouldBe(issued.Invitation.Email);
        resent.Status.ShouldBe("pending");
        resent.CreatedAt.ShouldBe(issued.Invitation.CreatedAt, TimeSpan.FromMilliseconds(1));
        resent.ExpiresAt.ShouldBeInRange(before.AddDays(7).AddSeconds(-5), DateTimeOffset.UtcNow.AddDays(7).AddSeconds(5));
        (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.InvitationProperties);
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).ExpiresAt.ShouldBe(resent.ExpiresAt, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task ResendInvitation_WhenPending_PublishesNewToken()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);

        InvitationIssued[] events = [.. Factory.Events.OfType<InvitationIssued>().Where(e => e.InvitationId == issued.Invitation.Id)];
        events.Length.ShouldBe(2);
        events[1].Token.ShouldNotBe(issued.Token);
        events[1].Token.Length.ShouldBe(43);
        events[1].Email.ShouldBe(issued.Invitation.Email);
    }

    [Fact]
    public async Task ResendInvitation_ReplacesTheLink()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        string newToken = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == issued.Invitation.Id).Token;

        await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
        (await (await Factory.PreviewInvitationAsync(newToken)).ShouldHaveJsonAsync<InvitationPreview>()).Status.ShouldBe("pending");
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
        await (await Factory.AcceptInvitationAsync(newToken)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ResendInvitation_WhenExpired_Returns200AndIsPendingAgain()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.ExpireInvitationAsync(issued.Invitation.Id);

        Invitation resent = await (await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct)).ShouldHaveJsonAsync<Invitation>();

        resent.Status.ShouldBe("pending");
        resent.ExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow.AddDays(6));
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("pending");
        string newToken = Factory.Events.Last<InvitationIssued>(e => e.InvitationId == issued.Invitation.Id).Token;
        await (await Factory.AcceptInvitationAsync(newToken)).ShouldHaveStatusAsync(HttpStatusCode.Created);
    }

    [Fact]
    public async Task ResendInvitation_WhenAccepted_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        Factory.Events.OfType<InvitationIssued>().Count(e => e.InvitationId == issued.Invitation.Id).ShouldBe(1);
    }

    [Fact]
    public async Task ResendInvitation_WhenCancelled_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("cancelled");
        await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResendInvitation_OfAnotherTrainer_Returns404()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser other = await CreateTrainerAsync();
        IssuedInvitation theirs = await InviteAsync(other);

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(theirs.Invitation.Id), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
        Factory.Events.OfType<InvitationIssued>().Count(e => e.InvitationId == theirs.Invitation.Id).ShouldBe(1);
        await (await Factory.PreviewInvitationAsync(theirs.Token)).ShouldHaveStatusAsync(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ResendInvitation_WithUnknownId_Returns404()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.PostAsync(IdentityRoutes.Resend(Guid.CreateVersion7()), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task ResendInvitation_WithoutToken_Returns401()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await CreateAnonymousClient().PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task ResendInvitation_AsClient_Returns403()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await clients[0].Client.PostAsync(IdentityRoutes.Resend(issued.Invitation.Id), content: null, Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
    }
}
