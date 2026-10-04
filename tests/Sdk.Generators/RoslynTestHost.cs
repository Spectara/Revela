using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Tests.Sdk.Generators;

/// <summary>
/// Compiles test sources against the real SDK and framework assemblies and runs analyzers
/// with an MSBuild <c>PackageType</c> as the build would pass it.
/// </summary>
internal static class RoslynTestHost
{
    private static readonly Lazy<IReadOnlyList<MetadataReference>> LazyReferences = new(CreateReferences);

    public static IReadOnlyList<MetadataReference> References => LazyReferences.Value;

    public static CSharpCompilation Compile(string source, string assemblyName = "Sample.Plugin") =>
        CSharpCompilation.Create(
            assemblyName,
            [CSharpSyntaxTree.ParseText(source)],
            References,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

    public static async Task<ImmutableArray<Diagnostic>> AnalyzeAsync(
        Compilation compilation,
        DiagnosticAnalyzer analyzer,
        string? packageType,
        ImmutableArray<AdditionalText> additionalFiles = default)
    {
        var options = new AnalyzerOptions(
            additionalFiles.IsDefault ? [] : additionalFiles,
            new PackageTypeOptionsProvider(packageType));

        return await compilation
            .WithAnalyzers([analyzer], options)
            .GetAnalyzerDiagnosticsAsync();
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
}

/// <summary>
/// Exposes <c>build_property.PackageType</c> the way <c>CompilerVisibleProperty</c> does.
/// </summary>
internal sealed class PackageTypeOptionsProvider(string? packageType) : AnalyzerConfigOptionsProvider
{
    public override AnalyzerConfigOptions GlobalOptions { get; } = new PackageTypeOptions(packageType);

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => PackageTypeOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => PackageTypeOptions.Empty;

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
