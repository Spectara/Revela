using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Spectara.Revela.Sdk.Generators;

/// <summary>
/// Reports plugin/theme code that binds configuration outside its own section.
/// </summary>
/// <remarks>
/// <para>
/// In a Revela plugin or theme (MSBuild <c>PackageType</c> <c>RevelaPlugin</c>/<c>RevelaTheme</c>)
/// every <c>AddOptions&lt;T&gt;().BindConfiguration(section)</c> must bind a type that this
/// assembly declares with <c>[RevelaConfig]</c>, and <c>section</c> must be a compile-time
/// constant equal to that attribute's section (in practice <c>T.Section</c>). Together with
/// the SDK's banned configuration APIs this keeps each plugin on its own <c>plugins:&lt;key&gt;</c>
/// node. Host assemblies are not checked: they own the root sections.
/// </para>
/// </remarks>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PluginConfigBindingAnalyzer : DiagnosticAnalyzer
{
    /// <summary>Diagnostic ID for binding a section the plugin does not own.</summary>
    public const string ForeignConfigBindingId = "REVELA003";

    private const string ConfigAttributeFullName = "Spectara.Revela.Sdk.Abstractions.RevelaConfigAttribute";
    private const string BindingExtensionsFullName = "Microsoft.Extensions.DependencyInjection.OptionsBuilderConfigurationExtensions";
    private const string BindConfigurationName = "BindConfiguration";
    private const string SectionParameterName = "configSectionPath";

    internal static readonly DiagnosticDescriptor ForeignConfigBinding = new(
        ForeignConfigBindingId,
        "Plugins may only bind their own configuration section",
        "BindConfiguration<{0}> is not allowed in a Revela plugin or theme: {1}. Bind a [RevelaConfig] type declared in this assembly to its own section, e.g. AddOptions<MyPluginConfig>().BindConfiguration(MyPluginConfig.Section).",
        "Revela.Configuration",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "A plugin reads only its own plugins:<key> node. Host settings arrive through the IOptions<T> the host registers; other plugins' settings are not readable.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = [ForeignConfigBinding];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(static start =>
        {
            if (!RevelaPackageType.IsPluginOrTheme(start.Options.AnalyzerConfigOptionsProvider.GlobalOptions))
            {
                return;
            }

            var bindingExtensions = start.Compilation.GetTypeByMetadataName(BindingExtensionsFullName);
            if (bindingExtensions is null)
            {
                return;
            }

            var configAttribute = start.Compilation.GetTypeByMetadataName(ConfigAttributeFullName);
            var assembly = start.Compilation.Assembly;

            start.RegisterOperationAction(
                ctx => AnalyzeInvocation(ctx, bindingExtensions, configAttribute, assembly),
                OperationKind.Invocation);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol bindingExtensions,
        INamedTypeSymbol? configAttribute,
        IAssemblySymbol assembly)
    {
        var invocation = (IInvocationOperation)context.Operation;
        var method = invocation.TargetMethod.ReducedFrom ?? invocation.TargetMethod;
        if (!string.Equals(method.Name, BindConfigurationName, StringComparison.Ordinal) ||
            !SymbolEqualityComparer.Default.Equals(method.ContainingType, bindingExtensions) ||
            invocation.TargetMethod.TypeArguments.Length != 1)
        {
            return;
        }

        var optionsType = invocation.TargetMethod.TypeArguments[0];
        var sectionArgument = invocation.Arguments.FirstOrDefault(a =>
            string.Equals(a.Parameter?.Name, SectionParameterName, StringComparison.Ordinal));

        var reason = GetViolation(optionsType, sectionArgument, configAttribute, assembly);
        if (reason is null)
        {
            return;
        }

        var location = sectionArgument?.Syntax.GetLocation() ?? invocation.Syntax.GetLocation();
        context.ReportDiagnostic(Diagnostic.Create(
            ForeignConfigBinding,
            location,
            optionsType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
            reason));
    }

    private static string? GetViolation(
        ITypeSymbol optionsType,
        IArgumentOperation? sectionArgument,
        INamedTypeSymbol? configAttribute,
        IAssemblySymbol assembly)
    {
        var typeName = optionsType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

        if (!SymbolEqualityComparer.Default.Equals(optionsType.ContainingAssembly, assembly))
        {
            return $"'{typeName}' is declared in '{optionsType.ContainingAssembly?.Name}', not in this assembly";
        }

        var ownSection = GetDeclaredSection(optionsType, configAttribute);
        if (ownSection is null)
        {
            return $"'{typeName}' has no [RevelaConfig] attribute";
        }

        if (sectionArgument?.Value.ConstantValue is not { HasValue: true, Value: string section })
        {
            return $"the section must be the constant {typeName}.Section (\"{ownSection}\")";
        }

        return string.Equals(section, ownSection, StringComparison.Ordinal)
            ? null
            : $"section \"{section}\" is not the section \"{ownSection}\" declared by '{typeName}'";
    }

    private static string? GetDeclaredSection(ITypeSymbol type, INamedTypeSymbol? configAttribute)
    {
        if (configAttribute is null)
        {
            return null;
        }

        foreach (var attribute in type.GetAttributes())
        {
            if (SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, configAttribute) &&
                attribute.ConstructorArguments.Length == 1 &&
                attribute.ConstructorArguments[0].Value is string section)
            {
                return section;
            }
        }

        return null;
    }
}
