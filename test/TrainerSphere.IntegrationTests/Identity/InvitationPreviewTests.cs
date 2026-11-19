using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

public sealed class InvitationPreviewTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    [Fact]
    public async Task PreviewInvitation_WhenPending_Returns200Preview()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage("Excited to work with you!"));

        HttpResponseMessage response = await Factory.PreviewInvitationAsync(issued.Token);

        InvitationPreview preview = await response.ShouldHaveJsonAsync<InvitationPreview>();
        preview.Email.ShouldBe(issued.Invitation.Email);
        preview.FirstName.ShouldBe(issued.Invitation.FirstName);
        preview.LastName.ShouldBe(issued.Invitation.LastName);
        preview.TrainerName.ShouldBe($"{trainer.FirstName} {trainer.LastName}");
        preview.Message.ShouldBe("Excited to work with you!");
        preview.Status.ShouldBe("pending");
        preview.ExpiresAt.ShouldBe(issued.Invitation.ExpiresAt, TimeSpan.FromMilliseconds(1));
        JsonElement body = (await response.ReadJsonAsync()).ShouldHaveExactProperties(IdentityRoutes.InvitationPreviewProperties);
        body.ShouldBeUtcTimestamp("expiresAt");
    }

    [Fact]
    public async Task PreviewInvitation_WithoutMessage_ReturnsNullMessage()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer, i => i.WithMessage(null));

        HttpResponseMessage response = await Factory.PreviewInvitationAsync(issued.Token);

        await response.ShouldHaveStatusAsync(HttpStatusCode.OK);
        (await response.ReadJsonAsync()).ShouldBeNullProperty("message");
    }

    [Fact]
    public async Task PreviewInvitation_WhenAccepted_Returns200WithAcceptedStatus()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await Factory.AcceptInvitationAsync(issued.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);

        InvitationPreview preview = await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldHaveJsonAsync<InvitationPreview>();

        preview.Status.ShouldBe("accepted");
        preview.Email.ShouldBe(issued.Invitation.Email);
    }

    [Fact]
    public async Task PreviewInvitation_WhenCancelled_Returns200WithCancelledStatus()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(issued.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);

        InvitationPreview preview = await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldHaveJsonAsync<InvitationPreview>();

        preview.Status.ShouldBe("cancelled");
    }

    [Fact]
    public async Task PreviewInvitation_WhenExpired_Returns200WithExpiredStatus()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);
        await Factory.ExpireInvitationAsync(issued.Invitation.Id);

        InvitationPreview preview = await (await Factory.PreviewInvitationAsync(issued.Token)).ShouldHaveJsonAsync<InvitationPreview>();

        preview.Status.ShouldBe("expired");
        preview.ExpiresAt.ShouldBeLessThan(DateTimeOffset.UtcNow);
    }

    [Theory]
    [InlineData("not-a-real-token")]
    [InlineData("AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task PreviewInvitation_WithUnknownToken_Returns404(string token)
    {
        HttpResponseMessage response = await Factory.PreviewInvitationAsync(token);

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task PreviewInvitation_WithInvitationId_Returns404()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        HttpResponseMessage response = await Factory.PreviewInvitationAsync(issued.Invitation.Id.ToString());

        await response.ShouldBeProblemAsync(HttpStatusCode.NotFound, "Invitation.NotFound");
    }

    [Fact]
    public async Task PreviewInvitation_DoesNotRevealInternalIds()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation issued = await InviteAsync(trainer);

        string body = await (await Factory.PreviewInvitationAsync(issued.Token)).Content.ReadAsStringAsync(Ct);

        body.ShouldNotContain(issued.Invitation.Id.ToString());
        body.ShouldNotContain(trainer.Id.ToString());
        body.ShouldNotContain(trainer.Email);
    }
}
