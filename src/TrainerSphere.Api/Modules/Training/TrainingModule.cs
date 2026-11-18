using TrainerSphere.Api.Host;
using TrainerSphere.Api.Modules.Training.Data;
using TrainerSphere.Api.Modules.Training.Features;
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
        services.AddSingleton(_ => ExerciseLibrary.LoadEmbedded());
        services.AddScoped<IStartupTask, ExerciseLibrarySeeder>();
        services.AddScoped<IDemoSeeder, TrainingDemoSeeder>();

        services.AddScoped<IProfileProjection, ProfileProjection>();
        services.AddScoped<IInvitationProjection, InvitationProjection>();
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IExerciseRepository, ExerciseRepository>();
        services.AddScoped<IWorkoutRepository, WorkoutRepository>();
        services.AddScoped<IProgramRepository, ProgramRepository>();
        services.AddScoped<IAssignmentRepository, AssignmentRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IBodyMetricRepository, BodyMetricRepository>();
    }
}
