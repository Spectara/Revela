using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using Scriban;
using Scriban.Runtime;

using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Generators;

namespace Spectara.Revela.Tests.Sdk.Generators;

[TestClass]
[TestCategory("Unit")]
public sealed class TemplateModelGeneratorTests
{
    private static readonly string[] ExpectedDerivedKeys = ["title", "width"];

    private static readonly Lazy<IReadOnlyList<MetadataReference>> References = new(CreateReferences);

    [TestMethod]
    public void ToScriptObject_PublicProperties_UseSnakeCaseKeys()
    {
        var result = Project("""
            [RevelaTemplateModel]
            public sealed class Model
            {
                public string DisplayName { get; init; } = "Alpine";
                public int PhotoID { get; init; } = 7;
                public string HTMLContent { get; init; } = "<p>";
            }
            """, "new Model()");

        Assert.AreEqual("Alpine", result["display_name"]);
        Assert.AreEqual(7, result["photo_id"]);
        Assert.AreEqual("<p>", result["html_content"]);
        Assert.HasCount(3, result);
    }

    [TestMethod]
    public void ToScriptObject_ScriptNameAndScriptIgnore_RenameAndSkipProperties()
    {
        var result = Project("""
            [RevelaTemplateModel]
            public sealed class Model
            {
                [ScriptName("title")]
                public string Heading { get; init; } = "Hello";

                [ScriptIgnore]
                public string Secret { get; init; } = "hidden";

                public static string Shared { get; } = "static";

                internal string Internal { get; init; } = "internal";
            }
            """, "new Model()");

        Assert.AreEqual("Hello", result["title"]);
        Assert.IsFalse(result.ContainsKey("heading"));
        Assert.IsFalse(result.ContainsKey("secret"));
        Assert.IsFalse(result.ContainsKey("shared"));
        Assert.IsFalse(result.ContainsKey("internal"));
        Assert.HasCount(1, result);
    }

    [TestMethod]
    public void ToScriptObject_NestedModelsListsAndDictionaries_AreProjectedWithoutReflection()
    {
        var result = Project("""
            [RevelaTemplateModel]
            public sealed class Child
            {
                public string Name { get; init; } = "";
            }

            [RevelaTemplateModel]
            public sealed class Model
            {
                public Child Main { get; init; } = new() { Name = "main" };
                public Child? Missing { get; init; }
                public IReadOnlyList<Child> Items { get; init; } = [new() { Name = "a" }, new() { Name = "b" }];
                public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string> { ["free"] = "Available" };
                public IReadOnlyDictionary<string, Child> ByKey { get; init; } = new Dictionary<string, Child> { ["k"] = new() { Name = "keyed" } };
            }
            """, "new Model()");

        var main = Assert.IsInstanceOfType<ScriptObject>(result["main"]);
        Assert.AreEqual("main", main["name"]);
        Assert.IsFalse(result.ContainsKey("missing"));
        var items = Assert.IsInstanceOfType<ScriptArray>(result["items"]);
        Assert.HasCount(2, items);
        Assert.AreEqual("b", Assert.IsInstanceOfType<ScriptObject>(items[1])["name"]);
        var labels = Assert.IsInstanceOfType<ScriptObject>(result["labels"]);
        Assert.AreEqual("Available", labels["free"]);
        var byKey = Assert.IsInstanceOfType<ScriptObject>(result["by_key"]);
        Assert.AreEqual("keyed", Assert.IsInstanceOfType<ScriptObject>(byKey["k"])["name"]);
    }

    [TestMethod]
    public void ToScriptObject_DerivedRecord_IncludesBasePropertiesFirst()
    {
        var result = Project("""
            public record BaseContent
            {
                public string Title { get; init; } = "base";
            }

            [RevelaTemplateModel]
            public sealed record Photo : BaseContent
            {
                public int Width { get; init; } = 640;
            }
            """, "new Photo()");

        CollectionAssert.AreEqual(ExpectedDerivedKeys, result.Keys.ToArray());
        Assert.AreEqual("base", result["title"]);
        Assert.AreEqual(640, result["width"]);
    }

    [TestMethod]
    public void ToScriptObject_AssemblyLevelAttribute_GeneratesForForeignType()
    {
        var result = Project("""
            [assembly: RevelaTemplateModel(typeof(Sample.Foreign))]

            namespace Sample;

            public sealed class Foreign
            {
                public string ExternalName { get; init; } = "foreign";
            }
            """, "new Foreign()", fileScopedNamespace: false);

        Assert.AreEqual("foreign", result["external_name"]);
    }

    [TestMethod]
    public void ToScriptObject_ResultRenderedByScriban_UsesGeneratedKeys()
    {
        var result = Project("""
            [RevelaTemplateModel]
            public sealed class GalleryModel
            {
                public required string DisplayName { get; init; }
            }
            """, "new GalleryModel { DisplayName = \"Alpine Gallery\" }");

        var context = new TemplateContext();
        context.PushGlobal(result);

        Assert.AreEqual("Alpine Gallery", Template.Parse("{{ display_name }}").Render(context));
    }

    private static ScriptObject Project(string models, string instance, bool fileScopedNamespace = true)
    {
        var source = $$"""
            using System.Collections.Generic;
            using Spectara.Revela.Sdk.Abstractions;
            using Spectara.Revela.Sdk.TemplateModels;
            {{(fileScopedNamespace ? "namespace Sample;" : string.Empty)}}
            {{models}}
            """;
        var probe = $$"""
            using Spectara.Revela.Sdk.TemplateModels;

            namespace Sample;

            public static class Probe
            {
                public static object Run() => ({{instance}}).ToScriptObject();
            }
            """;

        var compilation = CSharpCompilation.Create(
            "Sample.Models",
            [CSharpSyntaxTree.ParseText(source), CSharpSyntaxTree.ParseText(probe)],
            References.Value,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        GeneratorDriver driver = CSharpGeneratorDriver.Create(new TemplateModelGenerator());
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        Assert.IsEmpty(generatorDiagnostics);
        using var stream = new MemoryStream();
        var emit = output.Emit(stream);
        var errors = emit.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray();
        Assert.IsTrue(emit.Success, string.Join(Environment.NewLine, errors));

        stream.Position = 0;
        var context = new AssemblyLoadContext(nameof(TemplateModelGeneratorTests), isCollectible: true);
        try
        {
            var assembly = context.LoadFromStream(stream);
            var run = assembly.GetType("Sample.Probe")!.GetMethod("Run", BindingFlags.Public | BindingFlags.Static)!;
            return (ScriptObject)run.Invoke(null, null)!;
        }
        finally
        {
            context.Unload();
        }
    }

    private static IReadOnlyList<MetadataReference> CreateReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var trustedAssemblies = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? string.Empty;
        foreach (var path in trustedAssemblies.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            paths.Add(path);
        }

        paths.Add(typeof(RevelaTemplateModelAttribute).Assembly.Location);
        paths.Add(typeof(ScriptObject).Assembly.Location);

        return [.. paths.Select(path => MetadataReference.CreateFromFile(path))];
    }
}
