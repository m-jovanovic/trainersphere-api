using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.Api.Modules.Training;

internal sealed class TrainingModule : IModule
{
    public const string SchemaName = "training";

    public string Name => "Training";

    public string Schema => SchemaName;

    public MigrationSource Migrations { get; } = new(SchemaName, typeof(TrainingModule).Assembly, "TrainerSphere.Api.Modules.Training.Migrations");

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
    }
}
