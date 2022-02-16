using App.Utility;
using MediatR;
using Serilog;

LoggingUtility.Run(() =>
{
    WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

    builder.Services.AddSwaggerGen(o =>
    {
        o.UseApiEndpoints();

        o.EnableAnnotations();
    });

    builder.Services
        .AddControllers()
        .AddApplicationPart(Users.Api.AssemblyReference.Assembly);

    builder.Services.AddMediatR(Users.Application.AssemblyReference.Assembly);

    builder.Host.UseSerilog((_, loggerConfiguration) =>
        loggerConfiguration
            .ReadFrom
            .Configuration(builder.Configuration));

    WebApplication app = builder.Build();

    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI(o => o.DisplayRequestDuration());
    }

    app.UseSerilogRequestLogging();

    app.UseHttpsRedirection();

    app.UseAuthentication();

    app.UseAuthorization();

    app.MapControllers();

    app.Run();
});
