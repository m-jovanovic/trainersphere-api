using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.Api.Modules.Notifications;

internal sealed class NotificationsModule : IModule
{
    public const string SchemaName = "notifications";

    public string Name => "Notifications";

    public string Schema => SchemaName;

    public MigrationSource Migrations { get; } = new(SchemaName, typeof(NotificationsModule).Assembly, "TrainerSphere.Api.Modules.Notifications.Migrations");

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}
