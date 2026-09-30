using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Generators;

namespace Spectara.Revela.Tests.Sdk.Generators;

[TestClass]
[TestCategory("Unit")]
public sealed class PluginConfigSectionGeneratorTests
{
    private const string PluginPackageType = "RevelaPlugin";
    private const string ClaimAttributeName = "RevelaPluginConfigKeyAttribute";

    private static readonly string[] ExpectedSharedClaims = ["analytics", "serve"];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(CreateReferences);

    [TestMethod]
    [DataRow("plugins:serve", "serve")]
    [DataRow("plugins:oneDrive", "oneDrive")]
    [DataRow("plugins:calendarFeeds2", "calendarFeeds2")]
    public void Run_ValidSectionInPluginAssembly_ReportsNothingAndEmitsClaim(string section, string expectedKey)
    {
        var result = Run(ConfigClass(section, section), PluginPackageType);

        Assert.IsEmpty(result.GeneratorDiagnostics);
        Assert.IsEmpty(result.CompilationErrors);
        Assert.AreEqual(expectedKey, Assert.ContainsSingle(result.Claims));
    }

    [TestMethod]
    [DataRow("plugins:my.plugin", DisplayName = "dotted key")]
    [DataRow("plugins:serve:advanced", DisplayName = "colon-nested key")]
    [DataRow("Spectara.Revela.Plugins.Serve", DisplayName = "missing prefix (package-ID style)")]
    [DataRow("serve", DisplayName = "missing prefix")]
    [DataRow("generate", DisplayName = "core section")]
    [DataRow("generate:sorting", DisplayName = "nested core section")]
    [DataRow("plugins:Serve", DisplayName = "uppercase first letter")]
    [DataRow("plugins:my_plugin", DisplayName = "underscore")]
    [DataRow("plugins:my/plugin", DisplayName = "slash")]
    [DataRow("plugins:", DisplayName = "empty key")]
    public void Run_InvalidSectionInPluginAssembly_ReportsInvalidPluginSection(string section)
    {
        var result = Run(ConfigClass(section, section), PluginPackageType);

        var diagnostic = Assert.ContainsSingle(result.GeneratorDiagnostics);
        Assert.AreEqual(PluginConfigSectionGenerator.InvalidPluginSectionId, diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains(section, diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
        Assert.IsEmpty(result.Claims);
    }

    [TestMethod]
    public void Run_InvalidSectionInThemeAssembly_ReportsInvalidPluginSection()
    {
        var result = Run(ConfigClass("lumina.extras", "lumina.extras"), "RevelaTheme");

        var diagnostic = Assert.ContainsSingle(result.GeneratorDiagnostics);
        Assert.AreEqual(PluginConfigSectionGenerator.InvalidPluginSectionId, diagnostic.Id);
    }

    [TestMethod]
    [DataRow("Dependency;RevelaPlugin")]
    [DataRow("RevelaPlugin, 1.0.0")]
    public void Run_PackageTypeListContainingRevelaPlugin_TreatsAssemblyAsPlugin(string packageType)
    {
        var result = Run(ConfigClass("generate", "generate"), packageType);

        var diagnostic = Assert.ContainsSingle(result.GeneratorDiagnostics);
        Assert.AreEqual(PluginConfigSectionGenerator.InvalidPluginSectionId, diagnostic.Id);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no PackageType")]
    [DataRow("Dependency", DisplayName = "regular library")]
    public void Run_CoreSectionInCoreAssembly_ReportsNothing(string? packageType)
    {
        var result = Run(ConfigClass("generate", "generate"), packageType);

        Assert.IsEmpty(result.GeneratorDiagnostics);
        Assert.IsEmpty(result.Claims);
    }

    [TestMethod]
    public void Run_AttributeAndSectionConstantDiffer_ReportsSectionConstantMismatch()
    {
        var result = Run(ConfigClass("plugins:serve", "plugins:server"), PluginPackageType);

        var diagnostic = Assert.ContainsSingle(result.GeneratorDiagnostics);
        Assert.AreEqual(PluginConfigSectionGenerator.SectionConstantMismatchId, diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("plugins:server", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [TestMethod]
    public void Run_AttributeAndSectionConstantDifferInCoreAssembly_ReportsSectionConstantMismatch()
    {
        var result = Run(ConfigClass("generate", "generat"), packageType: null);

        var diagnostic = Assert.ContainsSingle(result.GeneratorDiagnostics);
        Assert.AreEqual(PluginConfigSectionGenerator.SectionConstantMismatchId, diagnostic.Id);
    }

    [TestMethod]
    public void Run_SeveralClassesSharingKeys_EmitsOneClaimPerDistinctKey()
    {
        const string source = """
            using Spectara.Revela.Sdk.Abstractions;

            namespace Sample;

            [RevelaConfig("plugins:serve")]
            internal sealed class ServeConfig
            {
                public const string Section = "plugins:serve";
            }

            [RevelaConfig("plugins:serve")]
            internal sealed class ServeReaderConfig
            {
                public const string Section = "plugins:serve";
            }

            [RevelaConfig("plugins:analytics")]
            internal sealed class AnalyticsConfig
            {
                public const string Section = "plugins:analytics";
            }
            """;

        var result = Run(source, PluginPackageType);

        Assert.IsEmpty(result.GeneratorDiagnostics);
        Assert.IsEmpty(result.CompilationErrors);
        CollectionAssert.AreEqual(ExpectedSharedClaims, result.Claims.ToArray());
    }

    private static string ConfigClass(string attributeSection, string constSection) => $$"""
        using Spectara.Revela.Sdk.Abstractions;

        namespace Sample;

        [RevelaConfig("{{attributeSection}}")]
        internal sealed class SampleConfig
        {
            public const string Section = "{{constSection}}";

            public int Port { get; set; }
        }
        """;

    private static GeneratorRun Run(string source, string? packageType)
    {
        var compilation = CSharpCompilation.Create(
            "Sample.Plugin",
            [CSharpSyntaxTree.ParseText(source)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new PluginConfigSectionGenerator().AsSourceGenerator()],
            optionsProvider: new PackageTypeOptionsProvider(packageType));

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var diagnostics);

        var claims = output.Assembly.GetAttributes()
            .Where(a => a.AttributeClass?.Name == ClaimAttributeName)
            .Select(a => (string)a.ConstructorArguments[0].Value!)
            .ToImmutableArray();

        var errors = output.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToImmutableArray();

        return new GeneratorRun(diagnostics, errors, claims);
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            paths.Add(path);
        }

        // The SDK declares [RevelaConfig] and the claim attribute the generated code references.
        paths.Add(Path.Combine(AppContext.BaseDirectory, typeof(RevelaConfigAttribute).Assembly.GetName().Name + ".dll"));

        return [.. paths.Select(path => MetadataReference.CreateFromFile(path))];
    }

    private sealed record GeneratorRun(
        ImmutableArray<Diagnostic> GeneratorDiagnostics,
        ImmutableArray<Diagnostic> CompilationErrors,
        ImmutableArray<string> Claims);

    private sealed class PackageTypeOptionsProvider(string? packageType) : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new PackageTypeOptions(packageType);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => PackageTypeOptions.Empty;

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => PackageTypeOptions.Empty;
    }

    private sealed class PackageTypeOptions(string? packageType) : AnalyzerConfigOptions
    {
        public static PackageTypeOptions Empty { get; } = new(null);

        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = string.Equals(key, "build_property.PackageType", StringComparison.Ordinal) ? packageType : null;
            return value is not null;
        }
    }
}
