using TrainerSphere.Api.Host;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.AddTrainerSphere();

WebApplication app = builder.Build();

if (args.Contains("--migrate", StringComparer.Ordinal))
{
    await app.MigrateDatabaseAsync();
    return;
}

app.UseTrainerSphere();

await app.RunAsync();
