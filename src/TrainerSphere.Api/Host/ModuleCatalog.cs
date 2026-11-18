using TrainerSphere.Api.Modules.Identity;
using TrainerSphere.Api.Modules.Notifications;
using TrainerSphere.Api.Modules.Training;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.Api.Host;

internal static class ModuleCatalog
{
    public static IReadOnlyList<IModule> All { get; } =
    [
        new IdentityModule(),
        new TrainingModule(),
        new NotificationsModule(),
    ];
}
