using TrainerSphere.Api.Modules.Notifications.Data;
using TrainerSphere.Api.Modules.Notifications.Infrastructure;
using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;

namespace TrainerSphere.Api.Modules.Notifications;

/// <summary>
/// Sends emails in reaction to integration events from other modules. Has no HTTP API.
/// </summary>
internal sealed class NotificationsModule : IModule
{
    public const string SchemaName = "notifications";

    public string Name => "Notifications";

    public string Schema => SchemaName;

    public MigrationSource Migrations { get; } = new(SchemaName, typeof(NotificationsModule).Assembly, "TrainerSphere.Api.Modules.Notifications.Migrations");

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<SmtpOptions>()
            .Bind(configuration.GetSection(SmtpOptions.SectionName))
            .Validate(options => !string.IsNullOrWhiteSpace(options.Host) && options.Port > 0, "Smtp:Host and Smtp:Port are required.")
            .Validate(options => EmailAddresses.TryParse(options.From, out _), "Smtp:From must be a valid address, for example 'TrainerSphere <no-reply@example.com>'.")
            .ValidateOnStart();
        services.AddOptions<WebLinkOptions>()
            .Bind(configuration.GetSection(WebLinkOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.WebBaseUrl, UriKind.Absolute, out _), "App:WebBaseUrl must be an absolute URL.")
            .ValidateOnStart();

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<WebLinks>();
        services.AddScoped<EmailDispatcher>();
        services.AddScoped<RecipientRepository>();
    }
}
