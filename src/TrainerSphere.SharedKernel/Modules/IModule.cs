using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TrainerSphere.SharedKernel.Migrations;

namespace TrainerSphere.SharedKernel.Modules;

/// <summary>
/// A module of the modular monolith: owns one Postgres schema, its migrations and its services.
/// </summary>
public interface IModule
{
    string Name { get; }

    string Schema { get; }

    MigrationSource Migrations { get; }

    void AddServices(IServiceCollection services, IConfiguration configuration);
}
