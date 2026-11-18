using TrainerSphere.IntegrationTests.Builders;
using TrainerSphere.IntegrationTests.Contracts;
using TrainerSphere.IntegrationTests.Infrastructure;

namespace TrainerSphere.IntegrationTests.Identity;

internal static class InvitationFlows
{
    /// <summary>
    /// Moves the invitation past its expiry; the API has no way to reach that state without waiting 7 days.
    /// </summary>
    public static Task ExpireInvitationAsync(this ApiFactory factory, Guid invitationId) =>
        factory.ExecuteAsync("UPDATE identity.invitations SET expires_at = now() - interval '1 minute' WHERE id = @Id", new { Id = invitationId });

    public static Task<HttpResponseMessage> AcceptInvitationAsync(this ApiFactory factory, string token, AcceptInvitationRequest? request = null) =>
        factory.CreateAnonymousClient().PostJsonAsync(IdentityRoutes.Accept(token), request ?? new AcceptInvitationBuilder().Build());

    public static Task<HttpResponseMessage> PreviewInvitationAsync(this ApiFactory factory, string token) =>
        factory.CreateAnonymousClient().GetAsync(IdentityRoutes.Preview(token), TestContext.Current.CancellationToken);

    public static async Task<Paged<Invitation>> ListInvitationsAsync(this TestUser trainer, string query = "")
    {
        HttpResponseMessage response = await trainer.Client.GetAsync(IdentityRoutes.Invitations + query, TestContext.Current.CancellationToken);
        return await response.ShouldHaveJsonAsync<Paged<Invitation>>();
    }

    /// <summary>
    /// The invitation as the trainer's list reports it (the API has no GET by id for trainers).
    /// </summary>
    public static async Task<Invitation> GetInvitationAsync(this TestUser trainer, Guid invitationId) =>
        (await trainer.ListInvitationsAsync("?pageSize=100")).Items.Single(i => i.Id == invitationId);
}
