using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TrainerSphere.SharedKernel.Results;

namespace TrainerSphere.UnitTests.SharedKernel.Results;

/// <summary>
/// Compiles consumer code against the shared kernel assembly to prove exhaustiveness is enforced across assemblies.
/// With TreatWarningsAsErrors, CS8509 fails the build.
/// </summary>
public sealed class ExhaustivenessTests
{
    private const string Usings = "using TrainerSphere.SharedKernel.Results;\n";

    private const string AllErrorArms = """
        ValidationError => 400,
        DomainError => 400,
        UnauthorizedError => 401,
        ForbiddenError => 403,
        NotFoundError => 404,
        ConflictError => 409,
        UnexpectedError => 500,
        """;

    private static readonly Lazy<MetadataReference[]> References = new(LoadReferences);

    [Fact]
    public void Compiler_uses_CSharp_15() =>
        Assert.Equal(LanguageVersion.CSharp15, LanguageVersion.Latest.MapSpecifiedToEffectiveVersion());

    [Fact]
    public void Switch_over_every_error_kind_needs_no_discard() =>
        Assert.Empty(Compile($"static class C {{ static int M(Error e) => e switch {{ {AllErrorArms} }}; }}"));

    [Fact]
    public void Switch_missing_an_error_kind_reports_CS8509()
    {
        string arms = AllErrorArms.Replace("ConflictError => 409,", string.Empty, StringComparison.Ordinal);

        Diagnostic diagnostic = Assert.Single(Compile($"static class C {{ static int M(Error e) => e switch {{ {arms} }}; }}"));

        Assert.Equal("CS8509", diagnostic.Id);
        Assert.Contains("ConflictError", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Switch_over_result_cases_needs_no_discard() =>
        Assert.Empty(Compile("static class C { static int M(Result<int> r) => r switch { Success<int> s => s.Value, Error => -1 }; }"));

    [Fact]
    public void Switch_over_result_missing_error_reports_CS8509() =>
        Assert.Equal("CS8509", Assert.Single(Compile("static class C { static int M(Result<int> r) => r switch { Success<int> s => s.Value }; }")).Id);

    [Fact]
    public void Switch_over_result_with_nested_error_kinds_is_exhaustive() =>
        Assert.Empty(Compile($"static class C {{ static int M(Result<int> r) => r switch {{ Success<int> => 200, {AllErrorArms} }}; }}"));

    [Fact]
    public void Switch_over_result_with_nested_error_kinds_missing_one_reports_CS8509()
    {
        string arms = AllErrorArms.Replace("UnexpectedError => 500,", string.Empty, StringComparison.Ordinal);

        Diagnostic diagnostic = Assert.Single(Compile($"static class C {{ static int M(Result<int> r) => r switch {{ Success<int> => 200, {arms} }}; }}"));

        Assert.Equal("CS8509", diagnostic.Id);
        Assert.Contains("UnexpectedError", diagnostic.GetMessage(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [Fact]
    public void Switch_statement_is_not_checked_for_exhaustiveness() =>
        Assert.Empty(Compile("static class C { static void M(Result<int> r) { switch (r) { case Success<int>: break; } } }"));

    [Fact]
    public void Another_assembly_cannot_add_an_error_kind()
    {
        Diagnostic diagnostic = Assert.Single(Compile("sealed record PaymentError(string Code, string Message) : Error(Code, Message);"));

        Assert.Equal("CS9382", diagnostic.Id);
    }

    [Fact]
    public void Error_cannot_be_instantiated_directly() =>
        Assert.Equal("CS0144", Assert.Single(Compile("static class C { static Error M() => new Error(\"a\", \"b\"); }")).Id);

    private static MetadataReference[] LoadReferences() =>
        [
            .. ((string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!)
                .Split(Path.PathSeparator)
                .Where(path => path.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Append(typeof(Result).Assembly.Location)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(path => MetadataReference.CreateFromFile(path)),
        ];

    private static Diagnostic[] Compile(string code)
    {
        var compilation = CSharpCompilation.Create(
            "Consumer",
            [CSharpSyntaxTree.ParseText(Usings + code, new CSharpParseOptions(LanguageVersion.CSharp15))],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return [.. compilation.GetDiagnostics().Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)];
    }
}
