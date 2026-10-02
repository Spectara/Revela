using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

using Spectara.Revela.Sdk.Generators;

namespace Spectara.Revela.Tests.Sdk.Generators;

/// <summary>
/// Compiles every <c>csharp</c> block of the SDK package readme (src/Sdk/README.md)
/// against the real SDK with the SDK's own generators, so the published examples
/// cannot drift from the API again.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed partial class SdkReadmeSnippetTests
{
    // Plugin projects use <ImplicitUsings>enable</ImplicitUsings>; snippets rely on the same set.
    private const string ImplicitUsings = """
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Threading;
        global using System.Threading.Tasks;
        """;

    [TestMethod]
    public void Readme_CSharpSnippets_CompileAgainstTheSdk()
    {
        var readme = File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Docs", "Sdk.README.md"));
        var snippets = CSharpBlock().Matches(readme).Select(match => match.Groups["code"].Value).ToList();
        Assert.IsGreaterThanOrEqualTo(3, snippets.Count, "Expected the README to contain C# examples.");

        var compilation = CSharpCompilation.Create(
            "Readme.Snippets",
            [CSharpSyntaxTree.ParseText(ImplicitUsings), .. snippets.Select(snippet => CSharpSyntaxTree.ParseText(snippet))],
            CreateReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new TemplateModelGenerator().AsSourceGenerator(), new PluginConfigSectionGenerator().AsSourceGenerator()],
            optionsProvider: new PluginPackageOptionsProvider());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        var errors = generatorDiagnostics.Concat(output.GetDiagnostics())
            .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
            .Select(diagnostic => diagnostic.ToString())
            .ToImmutableArray();
        Assert.IsEmpty(errors, string.Join(Environment.NewLine, errors));
    }

    [GeneratedRegex(@"```csharp\r?\n(?<code>.*?)```", RegexOptions.Singleline)]
    private static partial Regex CSharpBlock();

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        return [.. trustedAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => MetadataReference.CreateFromFile(path))];
    }

    private sealed class PluginPackageOptionsProvider : AnalyzerConfigOptionsProvider
    {
        public override AnalyzerConfigOptions GlobalOptions { get; } = new PluginPackageOptions(isGlobal: true);

        public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => new PluginPackageOptions(isGlobal: false);

        public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => new PluginPackageOptions(isGlobal: false);
    }

    private sealed class PluginPackageOptions(bool isGlobal) : AnalyzerConfigOptions
    {
        public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
        {
            value = isGlobal && string.Equals(key, "build_property.PackageType", StringComparison.Ordinal)
                ? "RevelaPlugin"
                : null;
            return value is not null;
        }
    }
}
