using App.Utility;
using Serilog;

LoggingUtility.Run(() =>
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen();

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

    app.Run();
});
