using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

Log.Information("Starting up.");

try
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddEndpointsApiExplorer();

    builder.Services.AddSwaggerGen();

    builder.Logging.ClearProviders();

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
}
catch (Exception e)
{
    Log.Fatal(e, "Unhandled exception.");
}
finally
{
    Log.Information("Shutting down.");
    Log.CloseAndFlush();
}
