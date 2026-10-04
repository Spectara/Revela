using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Spectara.Revela.Sdk.Generators;

namespace Spectara.Revela.Tests.Sdk.Generators;

/// <summary>
/// Runs the real BannedApiAnalyzers (RS0030) with the SDK's plugin ban list, exactly as
/// <c>Spectara.Revela.Sdk.targets</c> wires them into plugin and theme projects.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class BannedPluginApiTests
{
    private const string BannedSymbolsFileName = "BannedSymbols.RevelaPlugin.txt";
    private const string BannedApiId = "RS0030";

    private static readonly string BannedSymbolsPath = Path.Combine(AppContext.BaseDirectory, "Sdk", BannedSymbolsFileName);

    private static readonly Lazy<ImmutableArray<DiagnosticAnalyzer>> BannedApiAnalyzers = new(LoadBannedApiAnalyzers);

    [TestMethod]
    public void BannedSymbols_EveryEntry_ResolvesToASymbol()
    {
        var compilation = RoslynTestHost.Compile("namespace Sample;");

        var unresolved = ReadEntries()
            .Where(id => DocumentationCommentId.GetSymbolsForDeclarationId(id, compilation).IsEmpty)
            .ToList();

        Assert.IsNotEmpty(ReadEntries());
        Assert.IsEmpty(unresolved, string.Join(Environment.NewLine, unresolved));
    }

    [TestMethod]
    [DataRow("var value = provider.GetRequiredService<IConfiguration>()[\"plugins:serve:port\"];", DisplayName = "IConfiguration")]
    [DataRow("IConfigurationSection section = null!; _ = section.Value;", DisplayName = "IConfigurationSection")]
    [DataRow("IConfiguration configuration = null!; _ = configuration.GetValue<int>(\"plugins:serve:port\");", DisplayName = "ConfigurationBinder")]
    [DataRow("IConfiguration configuration = null!; services.Configure<SampleConfig>(configuration);", DisplayName = "Configure<T>(IConfiguration)")]
    [DataRow("IConfiguration configuration = null!; services.AddOptions<SampleConfig>().Bind(configuration);", DisplayName = "OptionsBuilder.Bind")]
    [DataRow("IConfigurationBuilder builder = null!; _ = builder.Sources;", DisplayName = "IConfigurationBuilder")]
    public async Task Analyze_PluginUsesConfigurationApi_ReportsBannedApi(string statement)
    {
        var diagnostics = await AnalyzeAsync(statement);

        Assert.IsTrue(diagnostics.Any(d => d.Id == BannedApiId), $"Expected {BannedApiId} for: {statement}");
    }

    [TestMethod]
    public async Task Analyze_PluginUsesOwnOptions_ReportsNothing()
    {
        var diagnostics = await AnalyzeAsync("""
            services.AddOptions<SampleConfig>().BindConfiguration(SampleConfig.Section);
            var monitor = provider.GetRequiredService<IOptionsMonitor<SampleConfig>>();
            _ = monitor.CurrentValue.Port;
            """);

        Assert.IsEmpty(diagnostics.Where(d => d.Id == BannedApiId).ToList());
    }

    [TestMethod]
    public async Task Analyze_ErrorInGeneratedBindingCode_IsSuppressed()
    {
        var generatedPath = Path.Combine(
            "obj", "Microsoft.Extensions.Configuration.Binder.SourceGeneration",
            "Microsoft.Extensions.Configuration.Binder.SourceGeneration.ConfigurationBindingGenerator",
            "BindingExtensions.g.cs");

        var diagnostics = await AnalyzeWithSuppressorAsync(generatedPath);

        Assert.IsTrue(diagnostics.Any(d => d.Id == BannedApiId && d.IsSuppressed), "RS0030 should be reported, then suppressed.");
        Assert.IsEmpty(diagnostics.Where(d => d.Id == BannedApiId && !d.IsSuppressed).ToList());
    }

    [TestMethod]
    public async Task Analyze_ErrorInHandWrittenCode_IsNotSuppressed()
    {
        var diagnostics = await AnalyzeWithSuppressorAsync(Path.Combine("src", "ConfigurationReader.cs"));

        var diagnostic = diagnostics.First(d => d.Id == BannedApiId);
        Assert.IsFalse(diagnostic.IsSuppressed);
        Assert.AreEqual(DiagnosticSeverity.Error, diagnostic.Severity);
    }

    /// <summary>
    /// Compiles a file that reads <c>IConfiguration</c> at <paramref name="path"/> with RS0030 as an
    /// error (<c>/warnaserror:RS0030</c>, as <c>Spectara.Revela.Sdk.targets</c> sets it) and the SDK suppressor active.
    /// </summary>
    private static async Task<ImmutableArray<Diagnostic>> AnalyzeWithSuppressorAsync(string path)
    {
        const string source = """
            using Microsoft.Extensions.Configuration;

            namespace Sample;

            internal static class ConfigurationReader
            {
                public static string? Read(IConfiguration configuration) => configuration["plugins:sample:port"];
            }
            """;

        var compilation = CSharpCompilation.Create(
            "Sample.Plugin",
            [CSharpSyntaxTree.ParseText(source, path: Path.GetFullPath(path))],
            RoslynTestHost.References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithSpecificDiagnosticOptions([new KeyValuePair<string, ReportDiagnostic>(BannedApiId, ReportDiagnostic.Error)]));

        var analyzers = BannedApiAnalyzers.Value.Add(new ConfigurationBindingSuppressor());
        var options = new AnalyzerOptions([new FileAdditionalText(BannedSymbolsPath)], new PackageTypeOptionsProvider("RevelaPlugin"));

        return await compilation
            .WithAnalyzers(analyzers, new CompilationWithAnalyzersOptions(options, onAnalyzerException: null, concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: true))
            .GetAnalyzerDiagnosticsAsync();
    }

    private static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(string statements)
    {
        var source = $$"""
            using System;
            using Microsoft.Extensions.Configuration;
            using Microsoft.Extensions.DependencyInjection;
            using Microsoft.Extensions.Options;
            using Spectara.Revela.Sdk.Abstractions;

            namespace Sample;

            [RevelaConfig("plugins:sample")]
            internal sealed class SampleConfig
            {
                public const string Section = "plugins:sample";

                [ConfigurationKeyName("port")]
                public int Port { get; set; }
            }

            internal static class Probe
            {
                public static void Run(IServiceCollection services, IServiceProvider provider)
                {
                    {{statements}}
                }
            }
            """;

        var compilation = RoslynTestHost.Compile(source);
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));

        return await RoslynTestHost.AnalyzeAsync(
            compilation,
            BannedApiAnalyzers.Value,
            packageType: "RevelaPlugin",
            additionalFiles: [new FileAdditionalText(BannedSymbolsPath)]);
    }

    private static List<string> ReadEntries() =>
        [.. File.ReadAllLines(BannedSymbolsPath)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith("//", StringComparison.Ordinal))
            .Select(line => line.Split(';', 2)[0].Trim())];

    private static ImmutableArray<DiagnosticAnalyzer> LoadBannedApiAnalyzers()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "BannedApiAnalyzers", "Microsoft.CodeAnalysis.CSharp.BannedApiAnalyzers.dll");
        var assembly = Assembly.LoadFrom(path);

        Type?[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types;
        }

        return [.. types
            .Where(t =>
                t is { IsAbstract: false } &&
                typeof(DiagnosticAnalyzer).IsAssignableFrom(t) &&
                t.GetCustomAttribute<DiagnosticAnalyzerAttribute>()?.Languages.Contains(LanguageNames.CSharp) == true)
            .Select(t => (DiagnosticAnalyzer)Activator.CreateInstance(t!)!)];
    }

    private sealed class FileAdditionalText(string path) : AdditionalText
    {
        public override string Path { get; } = path;

        public override SourceText GetText(CancellationToken cancellationToken = default) =>
            SourceText.From(File.ReadAllText(Path));
    }
}
