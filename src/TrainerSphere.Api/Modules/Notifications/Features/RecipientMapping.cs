using TrainerSphere.Api.Modules.Identity.Contracts;
using TrainerSphere.Api.Modules.Notifications.Domain;

namespace TrainerSphere.Api.Modules.Notifications.Features;

internal static class RecipientMapping
{
    public static Recipient Trainer(TrainerRegistered registered) =>
        new(registered.UserId, registered.Email, registered.FirstName, registered.LastName, Recipient.TrainerRole, TrainerId: null);

    public static Recipient Client(ClientRegistered registered) =>
        new(registered.UserId, registered.Email, registered.FirstName, registered.LastName, Recipient.ClientRole, registered.TrainerId);
}
