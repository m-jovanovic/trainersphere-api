using System.Net;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class InvitationCancelTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task CancelInvitation_WhenPending_Returns204AndMarksCancelled()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        Invitation cancelled = await trainer.GetInvitationAsync(issued.Invitation.Id);
        cancelled.Status.ShouldBe("cancelled");
        cancelled.AcceptedAt.ShouldBeNull();
        cancelled.ClientId.ShouldBeNull();
    }

    [Fact]
    public async Task CancelInvitation_WhenPending_LinkShowsCancelledAndCannotBeAccepted()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldHaveJsonAsync<InvitationPreview>()).Status.ShouldBe("cancelled");
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
    }

    [Fact]
    public async Task CancelInvitation_WhenAlreadyCancelled_Returns204()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        HttpResponseMessage response = await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        await response.ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("cancelled");
    }

    [Fact]
    public async Task CancelInvitation_WhenExpired_Returns204AndMarksCancelled()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.ExpireInvitationAsync(issued.Invitation.Id);

        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("cancelled");
    }

    [Fact]
    public async Task CancelInvitation_WhenAccepted_Returns409NotPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        HttpResponseMessage response = await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Conflict, "Invitation.NotPending");
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("accepted");
    }

    [Fact]
    public async Task CancelInvitation_OfAnotherTrainer_Returns404AndLeavesItPending()
    {
        TestUser trainer = await CreateTrainerAsync();
        TestUser other = await CreateTrainerAsync();
        IssuedInvitation theirs = await InviteAsync(other);

        HttpResponseMessage response = await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(theirs.Invitation.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
        (await other.GetInvitationAsync(theirs.Invitation.Id)).Status.ShouldBe("pending");
    }

    [Fact]
    public async Task CancelInvitation_WithUnknownId_Returns404()
    {
        TestUser trainer = await CreateTrainerAsync();

        HttpResponseMessage response = await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(Guid.CreateVersion7()), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task CancelInvitation_ThenInviteSameEmail_Returns201()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        IssuedInvitation again = await InviteAsync(trainer, i => i.WithEmail(issued.Invitation.Email));

        again.Invitation.Status.ShouldBe("pending");
    }

    [Fact]
    public async Task CancelInvitation_WithoutToken_Returns401()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await CreateAnonymousClient().DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Unauthorized, "Auth.Unauthorized");
    }

    [Fact]
    public async Task CancelInvitation_AsClient_Returns403()
    {
        (TestUser trainer, IReadOnlyList<TestUser> clients) = await CreateTrainerWithClientsAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await clients[0].Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct);

        await response.ShouldBeProblemAsync(HttpStatusCode.Forbidden, "Auth.Forbidden");
        (await trainer.GetInvitationAsync(issued.Invitation.Id)).Status.ShouldBe("pending");
    }
}
