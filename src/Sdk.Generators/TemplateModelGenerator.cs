using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Spectara.Revela.Sdk.Generators;

/// <summary>
/// Source generator that emits trim-safe <c>ToScriptObject()</c> extension
/// methods for classes / interfaces marked with <c>[RevelaTemplateModel]</c>
/// (class-level), and for foreign types opted in via
/// <c>[assembly: RevelaTemplateModel(typeof(ForeignType))]</c>.
/// </summary>
/// <remarks>
/// <para>
/// The emitted code copies each public instance property into a
/// <c>Scriban.Runtime.ScriptObject</c> using direct property access — no
/// runtime reflection. Property names are converted from PascalCase to
/// snake_case to match Scriban's default member renamer; override per property
/// with <c>[ScriptName("custom")]</c> or skip with <c>[ScriptIgnore]</c>.
/// </para>
/// <para>
/// Recursion: if a property type itself carries <c>[RevelaTemplateModel]</c>
/// (in any referenced assembly), the generator emits a nested
/// <c>.ToScriptObject()</c> call. Same for <c>IEnumerable&lt;T&gt;</c> of marked
/// types, which become a <c>ScriptArray</c> of <c>ScriptObject</c>s.
/// </para>
/// <para>
/// Dictionaries (<c>IReadOnlyDictionary&lt;string, _&gt;</c> /
/// <c>IDictionary&lt;string, _&gt;</c>) are iterated into nested
/// <c>ScriptObject</c>s — never dispatched through reflection.
/// </para>
/// </remarks>
[Generator]
public sealed class TemplateModelGenerator : IIncrementalGenerator
{
    private const string AttributeFullName = "Spectara.Revela.Sdk.Abstractions.RevelaTemplateModelAttribute";
    private const string IgnoreAttributeFullName = "Spectara.Revela.Sdk.Abstractions.ScriptIgnoreAttribute";
    private const string NameAttributeFullName = "Spectara.Revela.Sdk.Abstractions.ScriptNameAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Class-level [RevelaTemplateModel] on types declared in this assembly.
        var classLevel = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax or InterfaceDeclarationSyntax or RecordDeclarationSyntax,
                transform: static (ctx, _) => ctx.TargetSymbol as INamedTypeSymbol)
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        // Assembly-level [assembly: RevelaTemplateModel(typeof(ForeignType))].
        // The attribute returns the type via its TargetType property; we collect
        // the symbols here and merge with the class-level set.
        var assemblyLevel = context.CompilationProvider.Select(static (compilation, _) =>
        {
            var attrSymbol = compilation.GetTypeByMetadataName(AttributeFullName);
            if (attrSymbol is null)
            {
                return [];
            }

            var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
            foreach (var attr in compilation.Assembly.GetAttributes())
            {
                if (!SymbolEqualityComparer.Default.Equals(attr.AttributeClass, attrSymbol))
                {
                    continue;
                }

                if (attr.ConstructorArguments.Length == 1 &&
                    attr.ConstructorArguments[0].Value is INamedTypeSymbol target)
                {
                    builder.Add(target);
                }
            }

            return builder.ToImmutable();
        });

        var all = classLevel.Collect()
            .Combine(assemblyLevel)
            .Select(static (pair, _) =>
            {
                // Deduplicate by full metadata name.
                var seen = new HashSet<string>(StringComparer.Ordinal);
                var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
                foreach (var sym in pair.Left.Concat(pair.Right))
                {
                    var key = sym.ToDisplayString();
                    if (seen.Add(key))
                    {
                        builder.Add(sym);
                    }
                }
                return builder.ToImmutable();
            });

        context.RegisterSourceOutput(all, static (spc, types) =>
        {
            if (types.IsEmpty)
            {
                return;
            }

            // Build a lookup of "is type X also a [RevelaTemplateModel]?" so the
            // emitter can decide whether to recurse with .ToScriptObject() or
            // assign the value raw.
            var modelTypes = new HashSet<string>(
                types.Select(static t => t.ToDisplayString()),
                StringComparer.Ordinal);

            foreach (var type in types)
            {
                var source = GenerateExtension(type, modelTypes);
                var fileName = $"{SanitizeFileName(type.ToDisplayString())}.ScriptObject.g.cs";
                spc.AddSource(fileName, SourceText.From(source, Encoding.UTF8));
            }
        });
    }

    private static string GenerateExtension(INamedTypeSymbol type, HashSet<string> modelTypes)
    {
        var sb = new StringBuilder();
        var className = type.Name + "ScriptObjectExtensions";
        var paramType = type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine();
        sb.AppendLine("using System.Collections.Generic;");
        sb.AppendLine("using System.Diagnostics.CodeAnalysis;");
        sb.AppendLine("using Scriban.Runtime;");
        sb.AppendLine();
        sb.AppendLine("namespace Spectara.Revela.Sdk.TemplateModels;");
        sb.AppendLine();
        sb.AppendLine($"/// <summary>Auto-generated trim-safe Scriban projection for <see cref=\"{paramType}\"/>.</summary>");
        sb.AppendLine($"internal static class {className}");
        sb.AppendLine("{");
        sb.AppendLine($"    /// <summary>Builds a <see cref=\"ScriptObject\"/> from the public properties of a <see cref=\"{paramType}\"/>.</summary>");
        sb.AppendLine($"    public static ScriptObject ToScriptObject(this {paramType} value)");
        sb.AppendLine("    {");
        sb.AppendLine("        var so = new ScriptObject();");

        foreach (var prop in EnumerateScriptableProperties(type))
        {
            EmitPropertyAssignment(sb, prop, modelTypes);
        }

        sb.AppendLine("        return so;");
        sb.AppendLine("    }");
        sb.AppendLine();
        sb.AppendLine($"    /// <summary>Builds a <see cref=\"ScriptArray\"/> by projecting each element via <see cref=\"ToScriptObject({paramType})\"/>.</summary>");
        sb.AppendLine($"    public static ScriptArray ToScriptArray(this IEnumerable<{paramType}> values)");
        sb.AppendLine("    {");
        sb.AppendLine("        var arr = new ScriptArray();");
        sb.AppendLine("        foreach (var item in values)");
        sb.AppendLine("        {");
        sb.AppendLine("            arr.Add(item?.ToScriptObject());");
        sb.AppendLine("        }");
        sb.AppendLine("        return arr;");
        sb.AppendLine("    }");
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static IEnumerable<IPropertySymbol> EnumerateScriptableProperties(INamedTypeSymbol type)
    {
        // Walk up the inheritance chain so derived records (e.g. ImageContent : GalleryContent)
        // also expose inherited properties. Order: base first, then derived (templates rely on this).
        var chain = new Stack<INamedTypeSymbol>();
        for (var current = type; current is not null && current.SpecialType != SpecialType.System_Object; current = current.BaseType)
        {
            chain.Push(current);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);

        // For interfaces, fall back to declared properties on the interface itself.
        if (type.TypeKind == TypeKind.Interface)
        {
            foreach (var prop in type.GetMembers().OfType<IPropertySymbol>())
            {
                if (IsScriptable(prop) && seen.Add(prop.Name))
                {
                    yield return prop;
                }
            }
            yield break;
        }

        while (chain.Count > 0)
        {
            var t = chain.Pop();
            foreach (var prop in t.GetMembers().OfType<IPropertySymbol>())
            {
                if (IsScriptable(prop) && seen.Add(prop.Name))
                {
                    yield return prop;
                }
            }
        }
    }

    private static bool IsScriptable(IPropertySymbol prop)
    {
        if (prop.DeclaredAccessibility != Accessibility.Public ||
            prop.IsStatic ||
            prop.IsIndexer ||
            prop.GetMethod is null)
        {
            return false;
        }

        return !prop.GetAttributes().Any(a =>
            a.AttributeClass?.ToDisplayString() == IgnoreAttributeFullName);
    }

    private static void EmitPropertyAssignment(StringBuilder sb, IPropertySymbol prop, HashSet<string> modelTypes)
    {
        var key = GetScriptName(prop);
        var access = $"value.{prop.Name}";
        var propType = prop.Type;
        var isNullableRef = propType.NullableAnnotation == NullableAnnotation.Annotated && propType.IsReferenceType;
        var unwrapped = isNullableRef ? propType.WithNullableAnnotation(NullableAnnotation.NotAnnotated) : propType;

        // Recursion into nested marked types.
        if (unwrapped is INamedTypeSymbol named &&
            modelTypes.Contains(named.ToDisplayString()))
        {
            if (isNullableRef)
            {
                sb.AppendLine($"        if ({access} is not null) so[\"{key}\"] = {access}.ToScriptObject();");
            }
            else
            {
                sb.AppendLine($"        so[\"{key}\"] = {access}.ToScriptObject();");
            }
            return;
        }

        // Dictionary<string, _> / IReadOnlyDictionary<string, _> — iterate.
        if (TryGetStringKeyedDictionary(unwrapped, out var valueType))
        {
            sb.AppendLine($"        if ({access} is {{ Count: > 0 }})");
            sb.AppendLine("        {");
            sb.AppendLine($"            var __nested = new ScriptObject();");
            sb.AppendLine($"            foreach (var __kv in {access})");
            sb.AppendLine("            {");
            if (valueType is INamedTypeSymbol valueNamed && modelTypes.Contains(valueNamed.ToDisplayString()))
            {
                sb.AppendLine("                __nested[__kv.Key] = __kv.Value?.ToScriptObject();");
            }
            else
            {
                sb.AppendLine("                __nested[__kv.Key] = __kv.Value;");
            }
            sb.AppendLine("            }");
            sb.AppendLine($"            so[\"{key}\"] = __nested;");
            sb.AppendLine("        }");
            return;
        }

        // IEnumerable<T> where T is a marked model — project to ScriptArray of ScriptObjects.
        if (TryGetEnumerableElement(unwrapped, out var element) &&
            element is INamedTypeSymbol elementNamed &&
            modelTypes.Contains(elementNamed.ToDisplayString()))
        {
            if (isNullableRef)
            {
                sb.AppendLine($"        if ({access} is not null) so[\"{key}\"] = {access}.ToScriptArray();");
            }
            else
            {
                sb.AppendLine($"        so[\"{key}\"] = {access}.ToScriptArray();");
            }
            return;
        }

        // Default: pass through. Scriban natively handles primitives, strings,
        // IEnumerable, IDictionary (with string keys). JsonElement values flow
        // through unchanged so the existing post-processing in
        // ScribanTemplateEngine.ConvertJsonElementsInScriptObject converts them.
        if (isNullableRef)
        {
            sb.AppendLine($"        if ({access} is not null) so[\"{key}\"] = {access};");
        }
        else
        {
            sb.AppendLine($"        so[\"{key}\"] = {access};");
        }
    }

    private static bool TryGetStringKeyedDictionary(ITypeSymbol type, out ITypeSymbol valueType)
    {
        foreach (var iface in EnumerateSelfAndInterfaces(type))
        {
            if (iface is not INamedTypeSymbol named || named.TypeArguments.Length != 2)
            {
                continue;
            }

            var def = named.OriginalDefinition.ToDisplayString();
            if (def is "System.Collections.Generic.IReadOnlyDictionary<TKey, TValue>"
                    or "System.Collections.Generic.IDictionary<TKey, TValue>"
                    or "System.Collections.Generic.Dictionary<TKey, TValue>"
                && named.TypeArguments[0].SpecialType == SpecialType.System_String)
            {
                valueType = named.TypeArguments[1];
                return true;
            }
        }

        valueType = null!;
        return false;
    }

    private static bool TryGetEnumerableElement(ITypeSymbol type, out ITypeSymbol element)
    {
        // Strings are IEnumerable<char> — never project them as arrays.
        if (type.SpecialType == SpecialType.System_String)
        {
            element = null!;
            return false;
        }

        foreach (var iface in EnumerateSelfAndInterfaces(type))
        {
            if (iface is INamedTypeSymbol named &&
                named.OriginalDefinition.ToDisplayString() == "System.Collections.Generic.IEnumerable<T>")
            {
                element = named.TypeArguments[0];
                return true;
            }
        }

        element = null!;
        return false;
    }

    private static IEnumerable<ITypeSymbol> EnumerateSelfAndInterfaces(ITypeSymbol type)
    {
        yield return type;
        foreach (var iface in type.AllInterfaces)
        {
            yield return iface;
        }
    }

    private static string GetScriptName(IPropertySymbol prop)
    {
        var nameAttr = prop.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.ToDisplayString() == NameAttributeFullName);
        if (nameAttr is not null && nameAttr.ConstructorArguments.Length == 1 &&
            nameAttr.ConstructorArguments[0].Value is string name &&
            !string.IsNullOrEmpty(name))
        {
            return name;
        }

        return PascalToSnake(prop.Name);
    }

    private static string PascalToSnake(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return input;
        }

        var sb = new StringBuilder(input.Length + 4);
        for (var i = 0; i < input.Length; i++)
        {
            var c = input[i];
            if (i > 0 && char.IsUpper(c))
            {
                var prev = input[i - 1];
                var next = i + 1 < input.Length ? input[i + 1] : '\0';
                // Insert underscore on PascalCase boundaries:
                //   lowerUpper      → lower_upper      (camelCase boundary)
                //   UpperUpperLower → Upper_UpperLower (acronym → word boundary)
                if (char.IsLower(prev) || (char.IsUpper(prev) && char.IsLower(next)))
                {
                    sb.Append('_');
                }
            }
            sb.Append(char.ToLowerInvariant(c));
        }

        return sb.ToString();
    }

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
