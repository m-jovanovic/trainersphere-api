using System.Diagnostics;
using System.Threading.RateLimiting;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.RateLimiting;
using Scalar.AspNetCore;
using TrainerSphere.SharedKernel.Data;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;
using TrainerSphere.SharedKernel.Outbox;

namespace TrainerSphere.Api.Host;

internal static class HostingExtensions
{
    public const string AuthRateLimitPolicy = "auth";
    private const string CorsPolicy = "web";

    public static WebApplicationBuilder AddTrainerSphere(this WebApplicationBuilder builder)
    {
        IServiceCollection services = builder.Services;
        ConfigurationManager configuration = builder.Configuration;

        string connectionString = configuration.GetConnectionString("Database")
            ?? throw new InvalidOperationException("Connection string 'Database' is not configured.");

        services.AddSingleton(TimeProvider.System);
        services.ConfigureHttpJsonOptions(options => JsonDefaults.Configure(options.SerializerOptions));
        services.Configure<RouteHandlerOptions>(options => options.ThrowOnBadRequest = true);
        services.AddProblemDetails(options => options.CustomizeProblemDetails = Customize);
        services.AddExceptionHandler(options => options.StatusCodeSelector = exception =>
            exception is BadHttpRequestException badRequest ? badRequest.StatusCode : StatusCodes.Status500InternalServerError);

        services.AddDataAccess(connectionString);
        services.AddMessagingCore();
        services.Configure<MessagingOptions>(configuration.GetSection(MessagingOptions.SectionName));
        services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);

        services.AddSingleton<MigrationRunner>();
        services.AddScoped<IStartupTask, MigrationStartupTask>();

        foreach (IModule module in ModuleCatalog.All)
        {
            services.AddSingleton(module.Migrations);
            services.AddModuleHandlers(typeof(Program).Assembly, $"TrainerSphere.Api.Modules.{module.Name}", module.Schema);
            services.AddMessageProcessor(module.Schema, MessageKind.Outbox);
            services.AddMessageProcessor(module.Schema, MessageKind.Inbox);
            module.AddServices(services, configuration);
        }

        services.AddHostedService<StartupTasksHostedService>();
        services.AddMessageProcessingService();

        services.AddAuthorizationBuilder().AddPermissionPolicies();
        services.AddSingleton<IAuthorizationHandler, PermissionAuthorizationHandler>();

        services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecuritySchemeTransformer>());
        services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", tags: ["ready"]);
        services.AddRateLimiter(options => ConfigureRateLimiting(options, configuration));

        string[] origins = configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
        services.AddCors(options => options.AddPolicy(CorsPolicy, policy =>
            policy.WithOrigins(origins).AllowAnyHeader().AllowAnyMethod().AllowCredentials()));

        return builder;
    }

    public static WebApplication UseTrainerSphere(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();
        app.UseCors(CorsPolicy);
        app.UseAuthentication();
        app.UseAuthorization();
        app.UseRateLimiter();

        if (app.Configuration.GetValue("OpenApi:Enabled", app.Environment.IsDevelopment()))
        {
            app.MapOpenApi();
            app.MapScalarApiReference(options => options.WithTitle("TrainerSphere API"));
        }

        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

        foreach (IEndpoint endpoint in EndpointDiscovery.Discover(typeof(Program).Assembly))
        {
            endpoint.MapEndpoint(app);
        }

        return app;
    }

    public static async Task MigrateDatabaseAsync(this WebApplication app)
    {
        MigrationRunner runner = app.Services.GetRequiredService<MigrationRunner>();
        await runner.RunAsync(CancellationToken.None);
    }

    private static void Customize(ProblemDetailsContext context)
    {
        context.ProblemDetails.Instance ??= context.HttpContext.Request.Path;
        context.ProblemDetails.Extensions["traceId"] =
            Activity.Current?.Id ?? context.HttpContext.Features.Get<IHttpActivityFeature>()?.Activity.Id ?? context.HttpContext.TraceIdentifier;

        if (!context.ProblemDetails.Extensions.ContainsKey(ResultHttpExtensions.CodeExtension))
        {
            context.ProblemDetails.Extensions[ResultHttpExtensions.CodeExtension] = context.ProblemDetails.Status switch
            {
                StatusCodes.Status400BadRequest => "BadRequest",
                StatusCodes.Status401Unauthorized => "Auth.Unauthorized",
                StatusCodes.Status403Forbidden => "Auth.Forbidden",
                StatusCodes.Status404NotFound => "NotFound",
                StatusCodes.Status405MethodNotAllowed => "MethodNotAllowed",
                StatusCodes.Status429TooManyRequests => "RateLimited",
                _ => "Server.Error",
            };
        }
    }

    private static void ConfigureRateLimiting(RateLimiterOptions options, IConfiguration configuration)
    {
        int permitLimit = configuration.GetValue("RateLimiting:AuthPermitLimitPerMinute", 10);

        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.AddPolicy(AuthRateLimitPolicy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = permitLimit, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        options.OnRejected = async (context, cancellationToken) =>
        {
            IProblemDetailsService problems = context.HttpContext.RequestServices.GetRequiredService<IProblemDetailsService>();
            await problems.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = context.HttpContext,
                ProblemDetails =
                {
                    Status = StatusCodes.Status429TooManyRequests,
                    Title = "Too Many Requests",
                    Detail = "Too many attempts. Try again in a minute.",
                    Extensions = { [ResultHttpExtensions.CodeExtension] = "RateLimited" },
                },
            });
            _ = cancellationToken;
        };
    }
}

internal static class EndpointDiscovery
{
    public static IEnumerable<IEndpoint> Discover(System.Reflection.Assembly assembly) =>
        assembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IEndpoint).IsAssignableFrom(t))
            .OrderBy(t => t.FullName, StringComparer.Ordinal)
            .Select(t => (IEndpoint)Activator.CreateInstance(t)!);
}
