using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Identity.Data;
using TrainerSphere.Api.Modules.Identity.Domain;
using TrainerSphere.Api.Modules.Identity.Infrastructure;
using TrainerSphere.SharedKernel.Domain;
using TrainerSphere.SharedKernel.Messaging;

namespace TrainerSphere.Api.Modules.Identity.Features.DemoSeed;

/// <summary>
/// The demo trainer, three clients who joined through accepted invitations six weeks ago, and one pending invitation.
/// </summary>
internal sealed class IdentityDemoSeeder(
    UserRepository users,
    InvitationRepository invitations,
    PasswordHasher hasher,
    IEventBus eventBus,
    TimeProvider timeProvider) : IDemoSeeder
{
    public int Order => 10;

    public async Task<bool> SeedAsync(CancellationToken cancellationToken)
    {
        if (await users.EmailExistsAsync(DemoAccounts.TrainerEmail, cancellationToken))
        {
            return false;
        }

        DateTime now = timeProvider.UtcNow();
        string passwordHash = hasher.Hash(DemoAccounts.Password);
        DateTime trainerJoined = now.AddDays(-56);

        var trainer = User.RegisterTrainer(Ids.New(timeProvider), DemoAccounts.TrainerEmail, passwordHash, "Maya", "Novak", DemoAccounts.TimeZone, trainerJoined);
        await users.TryAddAsync(trainer, cancellationToken);
        await eventBus.PublishAsync(
            new TrainerRegistered(Ids.New(timeProvider), trainerJoined, trainer.Id, trainer.Email, trainer.FirstName, trainer.LastName, trainer.TimeZone),
            cancellationToken);

        for (int i = 0; i < DemoAccounts.Clients.Count; i++)
        {
            (string email, string firstName, string lastName) = DemoAccounts.Clients[i];
            DateTime invited = now.AddDays(-44 + i);
            DateTime joined = invited.AddHours(5);

            var invitation = Invitation.Issue(Ids.New(timeProvider), trainer.Id, email, firstName, lastName, "Excited to work with you!", SecureTokens.Hash(SecureTokens.Create()), invited);
            var client = User.RegisterClient(Ids.New(timeProvider), trainer.Id, email, passwordHash, firstName, lastName, DemoAccounts.TimeZone, joined);
            _ = invitation.Accept(client.Id, joined);

            await users.TryAddAsync(client, cancellationToken);
            await invitations.AddAsync(invitation, cancellationToken);
            await eventBus.PublishAsync(
                new ClientRegistered(Ids.New(timeProvider), joined, client.Id, trainer.Id, invitation.Id, client.Email, client.FirstName, client.LastName, client.TimeZone),
                cancellationToken);
        }

        string token = SecureTokens.Create();
        DateTime pendingSince = now.AddDays(-1);
        var pending = Invitation.Issue(Ids.New(timeProvider), trainer.Id, DemoAccounts.PendingInviteEmail, "Riley", "Morgan", "Hi Riley, here's your TrainerSphere invite. See you Monday!", SecureTokens.Hash(token), pendingSince);
        await invitations.AddAsync(pending, cancellationToken);
        await eventBus.PublishAsync(
            new InvitationIssued(Ids.New(timeProvider), pendingSince, pending.Id, pending.TrainerId, pending.Email, pending.FirstName, pending.LastName, trainer.FullName, pending.Message, token, pending.ExpiresAt),
            cancellationToken);

        return true;
    }
}
