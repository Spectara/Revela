using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Spectara.Revela.Sdk.Generators;

/// <summary>
/// Enforces the plugin configuration namespace and publishes each assembly's
/// claimed plugin keys to the host.
/// </summary>
/// <remarks>
/// <para>
/// Plugin and theme settings live below the host-owned node <c>plugins:&lt;key&gt;</c>.
/// For every class marked <c>[RevelaConfig]</c> this generator:
/// </para>
/// <list type="bullet">
/// <item>reports <see cref="InvalidPluginSection"/> when the assembly is a plugin or theme
/// (MSBuild <c>PackageType</c> <c>RevelaPlugin</c>/<c>RevelaTheme</c>, exposed through
/// <c>CompilerVisibleProperty</c> in <c>Spectara.Revela.Sdk.targets</c>) and the section is
/// not <c>plugins:&lt;key&gt;</c> with a key matching <c>^[a-z][a-zA-Z0-9]*$</c>. Core
/// assemblies (SDK, host features) keep their root sections such as <c>project</c>;</item>
/// <item>reports <see cref="SectionConstantMismatch"/> when the attribute argument and the
/// hand-written <c>public const string Section</c> differ;</item>
/// <item>emits <c>[assembly: RevelaPluginConfigKey("&lt;key&gt;")]</c> for every valid plugin
/// section so the host can detect duplicate claims before services are configured.</item>
/// </list>
/// </remarks>
[Generator]
public sealed class PluginConfigSectionGenerator : IIncrementalGenerator
{
    /// <summary>Diagnostic ID for a plugin/theme section outside <c>plugins:&lt;key&gt;</c>.</summary>
    public const string InvalidPluginSectionId = "REVELA001";

    /// <summary>Diagnostic ID for an attribute argument that differs from the <c>Section</c> const.</summary>
    public const string SectionConstantMismatchId = "REVELA002";

    private const string ConfigAttributeFullName = "Spectara.Revela.Sdk.Abstractions.RevelaConfigAttribute";
    private const string ClaimAttributeFullName = "Spectara.Revela.Sdk.Abstractions.RevelaPluginConfigKeyAttribute";
    private const string PluginPrefix = "plugins:";
    private const string Category = "Revela.Configuration";

    internal static readonly DiagnosticDescriptor InvalidPluginSection = new(
        InvalidPluginSectionId,
        "Plugin configuration must live under 'plugins:<key>'",
        "Configuration section '{0}' on '{1}' is not allowed in a Revela plugin or theme: {2}. Use 'plugins:<key>' where <key> matches ^[a-z][a-zA-Z0-9]*$ (camelCase letters and digits, e.g. 'plugins:myPlugin').",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Plugin and theme settings live below the host-owned 'plugins' node so they cannot clash with core sections and map cleanly to environment variables (SPECTARA__REVELA__PLUGINS__<KEY>__<SETTING>).");

    internal static readonly DiagnosticDescriptor SectionConstantMismatch = new(
        SectionConstantMismatchId,
        "[RevelaConfig] section and Section constant differ",
        "'{0}' declares [RevelaConfig(\"{1}\")] but its Section constant is \"{2}\"; both must be identical",
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "The hand-written Section constant is passed to BindConfiguration, so it must match the section declared by the attribute.");

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var configClasses = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                ConfigAttributeFullName,
                predicate: static (node, _) => node is ClassDeclarationSyntax,
                transform: static (ctx, _) => ConfigClassInfo.Create(ctx))
            .Where(static info => info is not null)
            .Select(static (info, _) => info!)
            .Collect();

        var isPackage = context.AnalyzerConfigOptionsProvider
            .Select(static (provider, _) => RevelaPackageType.IsPluginOrTheme(provider.GlobalOptions));

        var hasClaimAttribute = context.CompilationProvider
            .Select(static (compilation, _) => compilation.GetTypeByMetadataName(ClaimAttributeFullName) is not null);

        var input = configClasses.Combine(isPackage).Combine(hasClaimAttribute);

        context.RegisterSourceOutput(input, static (spc, data) =>
        {
            var classes = data.Left.Left;
            var package = data.Left.Right;
            var canClaim = data.Right;
            var claims = new SortedSet<string>(StringComparer.Ordinal);

            foreach (var info in classes)
            {
                if (info.SectionConstant is not null &&
                    !string.Equals(info.SectionConstant, info.Section, StringComparison.Ordinal))
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        SectionConstantMismatch,
                        info.SectionConstantLocation ?? info.AttributeLocation,
                        info.TypeName,
                        info.Section,
                        info.SectionConstant));
                }

                var reason = GetInvalidReason(info.Section, out var key);
                if (reason is null)
                {
                    claims.Add(key!);
                }
                else if (package)
                {
                    spc.ReportDiagnostic(Diagnostic.Create(
                        InvalidPluginSection,
                        info.AttributeLocation,
                        info.Section,
                        info.TypeName,
                        reason));
                }
            }

            if (canClaim && claims.Count > 0)
            {
                spc.AddSource("RevelaPluginConfigKeys.g.cs", SourceText.From(GenerateClaims(claims), Encoding.UTF8));
            }
        });
    }

    /// <summary>
    /// Returns <see langword="null"/> when <paramref name="section"/> is a valid
    /// <c>plugins:&lt;key&gt;</c> section, otherwise a human-readable reason.
    /// </summary>
    internal static string? GetInvalidReason(string section, out string? key)
    {
        key = null;
        if (!section.StartsWith(PluginPrefix, StringComparison.Ordinal))
        {
            return "plugin settings must be placed below 'plugins:'";
        }

        var candidate = section.Substring(PluginPrefix.Length);
        if (candidate.Length == 0)
        {
            return "the plugin key is empty";
        }

        if (candidate.IndexOf(':') >= 0)
        {
            return $"the key '{candidate}' is nested; declare one section per plugin key";
        }

        if (candidate.IndexOf('.') >= 0)
        {
            return $"the key '{candidate}' contains '.', which reads like a nested setting and breaks environment variables";
        }

        if (!IsValidKey(candidate))
        {
            return $"the key '{candidate}' must start with a lowercase letter and contain only letters and digits";
        }

        key = candidate;
        return null;
    }

    private static bool IsValidKey(string key)
    {
        if (key.Length == 0 || key[0] is < 'a' or > 'z')
        {
            return false;
        }

        for (var i = 1; i < key.Length; i++)
        {
            if (key[i] is not ((>= 'a' and <= 'z') or (>= 'A' and <= 'Z') or (>= '0' and <= '9')))
            {
                return false;
            }
        }

        return true;
    }

    private static string GenerateClaims(IEnumerable<string> keys)
    {
        var sb = new StringBuilder();
        sb.AppendLine("// <auto-generated/>");
        foreach (var key in keys)
        {
            sb.AppendLine($"[assembly: global::{ClaimAttributeFullName}(\"{key}\")]");
        }

        return sb.ToString();
    }

    private sealed class ConfigClassInfo : IEquatable<ConfigClassInfo>
    {
        private ConfigClassInfo(string typeName, string section, Location attributeLocation, string? sectionConstant, Location? sectionConstantLocation)
        {
            TypeName = typeName;
            Section = section;
            AttributeLocation = attributeLocation;
            SectionConstant = sectionConstant;
            SectionConstantLocation = sectionConstantLocation;
        }

        public string TypeName { get; }

        public string Section { get; }

        public Location AttributeLocation { get; }

        public string? SectionConstant { get; }

        public Location? SectionConstantLocation { get; }

        public static ConfigClassInfo? Create(GeneratorAttributeSyntaxContext ctx)
        {
            if (ctx.TargetSymbol is not INamedTypeSymbol type || ctx.Attributes.Length == 0)
            {
                return null;
            }

            var attribute = ctx.Attributes[0];
            if (attribute.ConstructorArguments.Length != 1 || attribute.ConstructorArguments[0].Value is not string section)
            {
                return null;
            }

            var attributeSyntax = attribute.ApplicationSyntaxReference?.GetSyntax() as AttributeSyntax;
            var attributeLocation = attributeSyntax?.ArgumentList?.Arguments.FirstOrDefault()?.GetLocation()
                ?? attributeSyntax?.GetLocation()
                ?? type.Locations.FirstOrDefault()
                ?? Location.None;

            string? constant = null;
            Location? constantLocation = null;
            foreach (var field in type.GetMembers("Section").OfType<IFieldSymbol>())
            {
                if (field.IsConst && field.ConstantValue is string value)
                {
                    constant = value;
                    constantLocation = field.Locations.FirstOrDefault();
                    break;
                }
            }

            return new ConfigClassInfo(type.ToDisplayString(), section, attributeLocation, constant, constantLocation);
        }

        public bool Equals(ConfigClassInfo? other) =>
            other is not null &&
            TypeName == other.TypeName &&
            Section == other.Section &&
            SectionConstant == other.SectionConstant &&
            AttributeLocation.Equals(other.AttributeLocation) &&
            Equals(SectionConstantLocation, other.SectionConstantLocation);

        public override bool Equals(object? obj) => Equals(obj as ConfigClassInfo);

        public override int GetHashCode() =>
            (TypeName.GetHashCode() * 397) ^ Section.GetHashCode();
    }
}
