using App.Extensions;
using App.Utility;
using Serilog;
using Users.Infrastructure.Configuration;

LoggingUtility.Run(() =>
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.InstallServicesFromAssembly(App.AssemblyReference.Assembly);

    UsersStartup.Initialize(builder.Services);

    builder.Host.UseSerilog((_, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom
            .Configuration(builder.Configuration));

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
});
