using System.Globalization;
using Microsoft.CodeAnalysis;
using Spectara.Revela.Sdk.Generators;

namespace Spectara.Revela.Tests.Sdk.Generators;

[TestClass]
[TestCategory("Unit")]
public sealed class PluginConfigBindingAnalyzerTests
{
    private const string PluginPackageType = "RevelaPlugin";

    [TestMethod]
    [DataRow("SampleConfig.Section", DisplayName = "Section constant")]
    [DataRow("\"plugins:sample\"", DisplayName = "equal literal")]
    public async Task Analyze_OwnConfigTypeBoundToOwnSection_ReportsNothing(string sectionExpression)
    {
        var diagnostics = await AnalyzeAsync(Bind("SampleConfig", sectionExpression), PluginPackageType);

        Assert.IsEmpty(diagnostics);
    }

    [TestMethod]
    public async Task Analyze_OwnConfigTypeBoundToForeignSection_ReportsForeignConfigBinding()
    {
        var diagnostics = await AnalyzeAsync(Bind("SampleConfig", "\"plugins:serve\""), PluginPackageType);

        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual(PluginConfigBindingAnalyzer.ForeignConfigBindingId, diagnostic.Id);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
        Assert.Contains("plugins:serve", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Analyze_TypeWithoutRevelaConfig_ReportsForeignConfigBinding()
    {
        var diagnostics = await AnalyzeAsync(Bind("UnmarkedConfig", "\"plugins:sample\""), PluginPackageType);

        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual(PluginConfigBindingAnalyzer.ForeignConfigBindingId, diagnostic.Id);
        Assert.Contains("UnmarkedConfig", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Analyze_ConfigTypeFromAnotherAssembly_ReportsForeignConfigBinding()
    {
        // PathsConfig is the host's [RevelaConfig("paths")] type; plugins read it through IOptions, never bind it.
        var diagnostics = await AnalyzeAsync(
            Bind("Spectara.Revela.Sdk.Configuration.PathsConfig", "Spectara.Revela.Sdk.Configuration.PathsConfig.Section"),
            PluginPackageType);

        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual(PluginConfigBindingAnalyzer.ForeignConfigBindingId, diagnostic.Id);
        Assert.Contains("PathsConfig", diagnostic.GetMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task Analyze_NonConstantSection_ReportsForeignConfigBinding()
    {
        var diagnostics = await AnalyzeAsync(Bind("SampleConfig", "SectionAtRuntime()"), PluginPackageType);

        var diagnostic = Assert.ContainsSingle(diagnostics);
        Assert.AreEqual(PluginConfigBindingAnalyzer.ForeignConfigBindingId, diagnostic.Id);
    }

    [TestMethod]
    public async Task Analyze_ForeignSectionInThemeAssembly_ReportsForeignConfigBinding()
    {
        var diagnostics = await AnalyzeAsync(Bind("SampleConfig", "\"generate\""), "RevelaTheme");

        Assert.AreEqual(PluginConfigBindingAnalyzer.ForeignConfigBindingId, Assert.ContainsSingle(diagnostics).Id);
    }

    [TestMethod]
    [DataRow(null, DisplayName = "no PackageType")]
    [DataRow("Dependency", DisplayName = "regular library")]
    public async Task Analyze_HostAssemblyBindingAnySection_ReportsNothing(string? packageType)
    {
        var diagnostics = await AnalyzeAsync(
            Bind("Spectara.Revela.Sdk.Configuration.PathsConfig", "\"paths\""),
            packageType);

        Assert.IsEmpty(diagnostics);
    }

    private static async Task<IReadOnlyList<Diagnostic>> AnalyzeAsync(string source, string? packageType)
    {
        var compilation = RoslynTestHost.Compile(source);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));

        return await RoslynTestHost.AnalyzeAsync(compilation, [new PluginConfigBindingAnalyzer()], packageType);
    }

    private static string Bind(string optionsType, string sectionExpression) => $$"""
        using Microsoft.Extensions.DependencyInjection;
        using Spectara.Revela.Sdk.Abstractions;

        namespace Sample;

        [RevelaConfig("plugins:sample")]
        internal sealed class SampleConfig
        {
            public const string Section = "plugins:sample";

            public int Port { get; set; }
        }

        internal sealed class UnmarkedConfig
        {
            public int Port { get; set; }
        }

        internal static class Registration
        {
            public static void Register(IServiceCollection services) =>
                services.AddOptions<{{optionsType}}>().BindConfiguration({{sectionExpression}});

            private static string SectionAtRuntime() => "plugins:" + "sample";
        }
        """;
}
