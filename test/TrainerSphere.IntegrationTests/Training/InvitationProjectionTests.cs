using System.Net;
using System.Text.Json;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Identity;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Training;

/// <summary>
/// The trainer dashboard reads pending invitations from the Training module's own projection, which Identity
/// events keep in sync: issuing tracks, accepting and cancelling remove.
/// </summary>
public sealed class InvitationProjectionTests(ApiFactory factory) : IntegrationTest(factory), IClassFixture<ApiFactory>
{
    private const string TrainerDashboard = "/api/dashboard/trainer";

    [Fact]
    public async Task Dashboard_PendingInvitations_TracksIssueAcceptAndCancel()
    {
        TestUser trainer = await CreateTrainerAsync();
        await Factory.WaitForMessagesAsync();
        (await PendingInvitationsAsync(trainer)).GetProperty("count").GetInt32().ShouldBe(0);

        IssuedInvitation cancelled = await InviteAsync(trainer);
        IssuedInvitation accepted = await InviteAsync(trainer);
        await Factory.WaitForMessagesAsync();

        JsonElement pending = await PendingInvitationsAsync(trainer);
        pending.GetProperty("count").GetInt32().ShouldBe(2);
        JsonElement item = pending.GetProperty("items").EnumerateArray().First(i => i.GetProperty("email").GetString() == cancelled.Invitation.Email);
        item.GetProperty("status").GetString().ShouldBe("pending");
        item.GetProperty("firstName").GetString().ShouldBe(cancelled.Invitation.FirstName);
        item.GetProperty("lastName").GetString().ShouldBe(cancelled.Invitation.LastName);
        item.GetProperty("acceptedAt").ValueKind.ShouldBe(JsonValueKind.Null);

        await (await trainer.Client.DeleteAsync(IdentityRoutes.Invitation(cancelled.Invitation.Id), Ct)).ShouldHaveStatusAsync(HttpStatusCode.NoContent);
        await Factory.WaitForMessagesAsync();
        (await PendingInvitationsAsync(trainer)).GetProperty("count").GetInt32().ShouldBe(1);

        await (await Factory.AcceptInvitationAsync(accepted.Token)).ShouldHaveStatusAsync(HttpStatusCode.Created);
        await Factory.WaitForMessagesAsync();
        (await PendingInvitationsAsync(trainer)).GetProperty("count").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Dashboard_PendingInvitations_MatchTheInvitationList_AfterAResend()
    {
        TestUser trainer = await CreateTrainerAsync();
        IssuedInvitation first = await InviteAsync(trainer);
        await InviteAsync(trainer);
        await Task.Delay(20, Ct);
        await (await trainer.Client.PostAsync(IdentityRoutes.Resend(first.Invitation.Id), content: null, Ct)).ShouldHaveStatusAsync(HttpStatusCode.OK);
        await Factory.WaitForMessagesAsync();

        JsonElement listed = (await (await trainer.Client.GetAsync($"{IdentityRoutes.Invitations}?status=pending", Ct)).ReadJsonAsync()).GetProperty("items");
        JsonElement pending = (await PendingInvitationsAsync(trainer)).GetProperty("items");

        Times(pending).ShouldBe(Times(listed));
        Times(pending)[^1].Id.ShouldBe(first.Invitation.Id);
    }

    private static (Guid Id, string? CreatedAt, string? ExpiresAt)[] Times(JsonElement items) =>
        [.. items.EnumerateArray().Select(i => (i.GetProperty("id").GetGuid(), i.GetProperty("createdAt").GetString(), i.GetProperty("expiresAt").GetString()))];

    private static async Task<JsonElement> PendingInvitationsAsync(TestUser trainer) =>
        (await (await trainer.Client.GetAsync(TrainerDashboard, Ct)).ReadJsonAsync()).GetProperty("pendingInvitations");
}
