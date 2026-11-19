using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using NetArchTest.Rules;
using Shouldly;
using TrainerSphere.Api.Host;
using TrainerSphere.SharedKernel.Http;
using TrainerSphere.SharedKernel.Messaging;
using TrainerSphere.SharedKernel.Migrations;
using TrainerSphere.SharedKernel.Modules;
using TrainerSphere.SharedKernel.Results;
using TestResult = NetArchTest.Rules.TestResult;

namespace TrainerSphere.UnitTests.Architecture;

/// <summary>
/// Module boundaries of the modular monolith: modules talk to each other only through <c>Modules.X.Contracts</c>
/// (integration events), each owns one schema, and features are vertical slices.
/// </summary>
public sealed partial class ModuleBoundaryTests
{
    private const string ModulesNamespace = "TrainerSphere.Api.Modules";

    private static readonly Assembly Api = typeof(ModuleCatalog).Assembly;
    private static readonly Assembly Kernel = typeof(Result).Assembly;

    public static TheoryData<string, string> ModulePairs()
    {
        TheoryData<string, string> pairs = [];

        foreach (IModule from in ModuleCatalog.All)
        {
            foreach (IModule to in ModuleCatalog.All.Where(m => m.Name != from.Name))
            {
                pairs.Add(from.Name, to.Name);
            }
        }

        return pairs;
    }

    public static TheoryData<string> ModuleNames() => [.. ModuleCatalog.All.Select(m => m.Name)];

    [Fact]
    public void SharedKernel_DoesNotDependOnTheApi()
    {
        TestResult result = Types.InAssembly(Kernel).ShouldNot().HaveDependencyOn("TrainerSphere.Api").GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModulePairs))]
    public void Module_UsesOtherModulesOnlyThroughTheirContracts(string module, string other)
    {
        string[] internals = [.. TypesOf(other).Where(t => !IsContract(t, other)).Select(t => t.FullName!)];

        TestResult result = Types.InAssembly(Api)
            .That().ResideInNamespace($"{ModulesNamespace}.{module}")
            .ShouldNot().HaveDependencyOnAny(internals)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue($"{module} must only use {other}.Contracts. {Describe(result)}");
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_AreSealedIntegrationEventsWithoutModuleInternals(string module)
    {
        Type[] contracts = [.. TypesOf(module).Where(t => IsContract(t, module) && !IsCompilerGenerated(t))];

        foreach (Type contract in contracts)
        {
            contract.IsSealed.ShouldBeTrue($"{contract.Name} must be sealed.");
            typeof(IIntegrationEvent).IsAssignableFrom(contract).ShouldBeTrue($"{contract.Name} must be an integration event.");
        }

        string[] internals = [.. TypesOf(module).Where(t => !IsContract(t, module)).Select(t => t.FullName!)];

        TestResult result = Types.InAssembly(Api)
            .That().ResideInNamespace($"{ModulesNamespace}.{module}.Contracts")
            .ShouldNot().HaveDependencyOnAny(internals)
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Domain_DoesNotDependOnFeaturesDataInfrastructureOrAspNetCore(string module)
    {
        string root = $"{ModulesNamespace}.{module}";

        TestResult result = Types.InAssembly(Api)
            .That().ResideInNamespace($"{root}.Domain")
            .ShouldNot().HaveDependencyOnAny($"{root}.Features", $"{root}.Data", $"{root}.Infrastructure", "Microsoft.AspNetCore", "Npgsql", "Dapper")
            .GetResult();

        result.IsSuccessful.ShouldBeTrue(Describe(result));
    }

    [Fact]
    public void Handlers_LiveInFeatureSlices_AndAreInternalAndSealed()
    {
        Type[] handlerDefinitions = [typeof(ICommandHandler<,>), typeof(IQueryHandler<,>), typeof(IIntegrationEventHandler<>), typeof(IDomainEventHandler<>)];

        Type[] handlers = [.. Api.GetTypes().Where(t => t.IsClass && t.GetInterfaces().Any(i => i.IsGenericType && handlerDefinitions.Contains(i.GetGenericTypeDefinition())))];

        handlers.ShouldNotBeEmpty();

        foreach (Type handler in handlers)
        {
            handler.Namespace.ShouldNotBeNull();
            FeatureSliceNamespace().IsMatch(handler.Namespace).ShouldBeTrue($"{handler.FullName} must live in Modules.<Module>.Features.<Slice>.");
            handler.IsSealed.ShouldBeTrue($"{handler.FullName} must be sealed.");
            handler.IsPublic.ShouldBeFalse($"{handler.FullName} must be internal.");
        }
    }

    [Fact]
    public void Endpoints_LiveInFeatureSlices_AndAreInternalAndSealed()
    {
        Type[] endpoints = [.. Api.GetTypes().Where(t => t.IsClass && typeof(IEndpoint).IsAssignableFrom(t))];

        endpoints.ShouldNotBeEmpty();

        foreach (Type endpoint in endpoints)
        {
            FeatureSliceNamespace().IsMatch(endpoint.Namespace ?? string.Empty).ShouldBeTrue($"{endpoint.FullName} must live in Modules.<Module>.Features.<Slice>.");
            endpoint.IsSealed.ShouldBeTrue($"{endpoint.FullName} must be sealed.");
            endpoint.IsPublic.ShouldBeFalse($"{endpoint.FullName} must be internal.");
        }
    }

    [Fact]
    public void FeatureSlices_DoNotDependOnEachOther()
    {
        string[] slices = Api.GetTypes()
            .Select(t => t.Namespace)
            .OfType<string>()
            .Select(ns => FeatureSliceNamespace().Match(ns))
            .Where(m => m.Success)
            .Select(m => m.Value)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        List<string> failures = [];

        foreach (string slice in slices)
        {
            string[] others = Api.GetTypes()
                .Where(t => t.Namespace is { } ns && slices.Any(s => s != slice && (ns == s || ns.StartsWith(s + ".", StringComparison.Ordinal))))
                .Select(t => t.FullName!)
                .ToArray();

            TestResult result = Types.InAssembly(Api).That().ResideInNamespace(slice).ShouldNot().HaveDependencyOnAny(others).GetResult();

            if (!result.IsSuccessful)
            {
                failures.Add($"{slice}: {Describe(result)}");
            }
        }

        failures.ShouldBeEmpty("Slices share code through the module's Domain, Data or Infrastructure, or the Features root.");
    }

    [Fact]
    public void ApiTypes_AreNotPublic_ExceptTheEntryPoint()
    {
        string[] publicTypes = [.. Api.GetExportedTypes().Select(t => t.FullName!).Where(n => n != "Program")];

        publicTypes.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Migrations_AreNumberedFromOneWithoutGaps(string module)
    {
        MigrationSource source = ModuleCatalog.All.Single(m => m.Name == module).Migrations;

        int[] versions = [.. source.Load().Select(m => m.Version)];

        versions.ShouldBe(Enumerable.Range(1, versions.Length).ToArray());
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Migrations_TouchOnlyTheModuleSchema(string module)
    {
        IModule owner = ModuleCatalog.All.Single(m => m.Name == module);
        string[] otherSchemas = [.. ModuleCatalog.All.Where(m => m.Name != module).Select(m => m.Schema)];

        foreach (Migration migration in owner.Migrations.Load())
        {
            foreach (string schema in otherSchemas)
            {
                Regex.IsMatch(migration.Sql, $@"\b{schema}\.", RegexOptions.IgnoreCase)
                    .ShouldBeFalse($"{module} V{migration.Version} references the {schema} schema.");
            }
        }
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void ModuleSql_QueriesOnlyTheModuleSchema(string module)
    {
        IModule owner = ModuleCatalog.All.Single(m => m.Name == module);
        string folder = Path.Combine(RepositoryRoot(), "src", "TrainerSphere.Api", "Modules", module);
        string[] otherSchemas = [.. ModuleCatalog.All.Where(m => m.Name != module).Select(m => m.Schema)];
        List<string> failures = [];

        foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
        {
            string source = File.ReadAllText(file);

            foreach (string schema in otherSchemas)
            {
                if (Regex.IsMatch(source, $@"\b(FROM|JOIN|INTO|UPDATE|TABLE)\s+{schema}\.", RegexOptions.IgnoreCase))
                {
                    failures.Add($"{Path.GetRelativePath(folder, file)} queries {schema} tables.");
                }
            }
        }

        failures.ShouldBeEmpty($"{owner.Name} must read other modules' data from its own copies kept in sync by integration events.");
    }

    private static Type[] TypesOf(string module) =>
        [.. Api.GetTypes().Where(t => t.Namespace is { } ns && (ns == $"{ModulesNamespace}.{module}" || ns.StartsWith($"{ModulesNamespace}.{module}.", StringComparison.Ordinal)))];

    private static bool IsContract(Type type, string module) =>
        type.Namespace is { } ns && (ns == $"{ModulesNamespace}.{module}.Contracts" || ns.StartsWith($"{ModulesNamespace}.{module}.Contracts.", StringComparison.Ordinal));

    private static bool IsCompilerGenerated(Type type) => type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

    private static string Describe(TestResult result) =>
        result.IsSuccessful ? string.Empty : "Violations: " + string.Join(", ", result.FailingTypeNames ?? []);

    private static string RepositoryRoot([CallerFilePath] string path = "")
    {
        DirectoryInfo? directory = new FileInfo(path).Directory;

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "TrainerSphere.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Repository root (TrainerSphere.slnx) not found.");
    }

    [GeneratedRegex(@"^TrainerSphere\.Api\.Modules\.[A-Za-z]+\.Features\.[A-Za-z]+")]
    private static partial Regex FeatureSliceNamespace();
}
