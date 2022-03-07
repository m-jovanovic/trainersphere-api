using App.Extensions;
using App.Utility;
using Infrastructure.Extensions;
using Serilog;

LoggingUtility.Run(() =>
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services
        .InstallServicesFromAssemblies(
            builder.Configuration,
            App.AssemblyReference.Assembly,
            Authorization.AssemblyReference.Assembly)
        .InstallModulesFromAssemblies(
            builder.Configuration,
            Modules.Users.Infrastructure.AssemblyReference.Assembly,
            Modules.Notifications.Infrastructure.AssemblyReference.Assembly);

    builder.Host.UseSerilogWithConfiguration();

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger().UseSwaggerUI();
    }

    app.UseSerilogRequestLogging()
        .UseHttpsRedirection()
        .UseAuthentication()
        .UseAuthorization();

    app.MapControllers();

    app.Run();
});
