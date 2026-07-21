using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Spectara.Revela.Sdk.Generators;

/// <summary>
/// Source generator that emits a <c>public static class &lt;Poco&gt;Keys</c> with
/// <c>public const string</c> fields for every configuration POCO marked with
/// <c>[RevelaConfig]</c> (the root types) and for every configuration class they
/// reference transitively through their properties (the nested types).
/// </summary>
/// <remarks>
/// <para>
/// Each emitted constant maps a POCO property name to its camelCase configuration
/// key (e.g. <c>ProjectConfigKeys.BaseUrl == "baseUrl"</c>). When a POCO carries a
/// hand-written <c>public const string Section</c>, its value is mirrored as
/// <c>Section</c> so writers can build the section-wrapper key from the same source.
/// </para>
/// <para>
/// The point is to make the raw-JSON config writers reference these constants
/// instead of hardcoded strings: a POCO property rename then breaks the writer at
/// compile time instead of silently writing to a dead key.
/// </para>
/// <para>
/// Nested recursion only descends into class types that are declared in source in
/// the same compilation (config POCOs live next to their roots). Primitives,
/// strings, enums, <c>Uri</c>, dictionaries and other collections are metadata types
/// and are therefore skipped. Collection element types are intentionally not
/// emitted. Only <c>const string</c> is produced — no reflection, AOT/trim-safe.
/// </para>
/// </remarks>
[Generator]
public sealed class ConfigKeysGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Spectara.Revela.Sdk.Abstractions.RevelaConfigAttribute";
    private const string KeysNamespace = "Spectara.Revela.Sdk.Configuration.Keys";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Root config POCOs: classes carrying [RevelaConfig] declared in this assembly.
        var roots = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol)
            .Where(static s => s is not null)
            .Select(static (s, _) => s!)
            .Collect();

        context.RegisterSourceOutput(roots, static (spc, rootTypes) =>
        {
            if (rootTypes.IsEmpty)
            {
                return;
            }

            // Collect the roots plus every config class reachable through their
            // property graph, deduplicated by full type name.
            var collected = CollectConfigTypes(rootTypes);

            foreach (var type in collected)
            {
                var source = GenerateKeysClass(type);
                var fileName = $"{SanitizeFileName(type.ToDisplayString())}.Keys.g.cs";
                spc.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    private static IReadOnlyList<INamedTypeSymbol> CollectConfigTypes(ImmutableArray<INamedTypeSymbol> roots)
    {
        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        var ordered = new List<INamedTypeSymbol>();
        var queue = new Queue<INamedTypeSymbol>();

        foreach (var root in roots)
        {
            if (seen.Add(root.ToDisplayString()))
            {
                queue.Enqueue(root);
                ordered.Add(root);
            }
        }

        while (queue.Count > 0)
        {
            var type = queue.Dequeue();
            foreach (var prop in EnumerateKeyProperties(type))
            {
                if (TryGetNestedConfigType(prop.Type, out var nested) &&
                    seen.Add(nested.ToDisplayString()))
                {
                    ordered.Add(nested);
                    queue.Enqueue(nested);
                }
            }
        }

        return ordered;
    }

    private static bool TryGetNestedConfigType(ITypeSymbol type, out INamedTypeSymbol nested)
    {
        // Unwrap nullable reference annotation (e.g. `SortingConfig?`).
        var unwrapped = type.NullableAnnotation == NullableAnnotation.Annotated && type.IsReferenceType
            ? type.WithNullableAnnotation(NullableAnnotation.NotAnnotated)
            : type;

        // Only named class types declared in source in this compilation qualify as
        // nested config POCOs. This naturally excludes string, enums, Uri,
        // dictionaries and other collections (all metadata types), and primitives.
        if (unwrapped is INamedTypeSymbol named &&
            named.TypeKind == TypeKind.Class &&
            named.SpecialType == SpecialType.None &&
            named.DeclaringSyntaxReferences.Length > 0)
        {
            nested = named;
            return true;
        }

        nested = null!;
        return false;
    }

    private static string GenerateKeysClass(INamedTypeSymbol type)
    {
        var className = type.Name + "Keys";
        var docType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine($"namespace {KeysNamespace};");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>Auto-generated configuration keys for <see cref=\"{docType}\"/>.</summary>");
        sb.AppendLine($"public static class {className}");
        sb.AppendLine("{");

        var emitted = new HashSet<string>(System.StringComparer.Ordinal);

        // Mirror the hand-written `public const string Section` when present.
        var section = GetSectionConstant(type);
        if (section is not null && emitted.Add("Section"))
        {
            sb.AppendLine("    /// <summary>Configuration section name.</summary>");
            sb.AppendLine($"    public const string Section = {Literal(section)};");
        }

        foreach (var prop in EnumerateKeyProperties(type))
        {
            if (!emitted.Add(prop.Name))
            {
                continue;
            }

            var key = ToCamelCase(prop.Name);
            sb.AppendLine($"    /// <summary>Configuration key for <c>{prop.Name}</c>.</summary>");
            sb.AppendLine($"    public const string {prop.Name} = {Literal(key)};");
        }

        sb.AppendLine("}");
        return sb.ToString();
    }

    private static IEnumerable<IPropertySymbol> EnumerateKeyProperties(INamedTypeSymbol type)
    {
        // Walk the inheritance chain (base first) so derived POCOs also expose
        // inherited properties. Config POCOs are usually sealed with no base, but
        // this keeps the generator robust.
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            chain.Push(current);
        }

        var seen = new HashSet<string>(System.StringComparer.Ordinal);
        while (chain.Count > 0)
        {
            var t = chain.Pop();
            foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (IsKeyProperty(prop) && seen.Add(prop.Name))
                {
                    yield return prop;
                }
            }
        }
    }

    private static bool IsKeyProperty(IPropertySymbol prop) =>
        prop.DeclaredAccessibility == Accessibility.Public &&
        !prop.IsStatic &&
        !prop.IsIndexer &&
        prop.GetMethod is not null;

    private static string? GetSectionConstant(INamedTypeSymbol type)
    {
        foreach (var member in type.GetMembers("Section").OfType<IFieldSymbol>())
        {
            if (member.IsConst &&
                member.Type.SpecialType == SpecialType.System_String &&
                member.ConstantValue is string value)
            {
                return value;
            }
        }

        return null;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }

    private static string Literal(string value) =>
        "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";

    private static string SanitizeFileName(string input)
    {
        var sb = new StringBuilder(input.Length);
        foreach (var c in input)
        {
            sb.Append(char.IsLetterOrDigit(c) || c == '_' || c == '.' ? c : '_');
        }

        return sb.ToString();
    }
}
