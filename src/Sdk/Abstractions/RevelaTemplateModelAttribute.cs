namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Marks a class as a Scriban template model. The Revela Source Generator emits
/// an extension method <c>ToScriptObject()</c> that builds a trim-safe
/// <c>Scriban.Runtime.ScriptObject</c> from the type's public properties.
/// </summary>
/// <remarks>
/// <para>
/// Two usage forms:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Class-level</b> — placed directly on the model class. The generator emits
/// the extension method into the same assembly. Use this for types you own.
/// </item>
/// <item>
/// <b>Assembly-level</b> — <c>[assembly: RevelaTemplateModel(typeof(ForeignType))]</c>
/// in an assembly that references Scriban. Use this to opt a foreign type
/// (typically an SDK type) into generation from a consumer assembly without
/// adding a Scriban dependency to the SDK.
/// </item>
/// </list>
/// <para>
/// Why this exists: Scriban's reflective <c>Import(object)</c> path is
/// structurally trim-incompatible (see
/// <see href="https://github.com/scriban/scriban/blob/master/site/docs/runtime/aot-support.md">
/// Scriban AOT support docs</see>). Passing a <c>ScriptObject</c> built from
/// statically known properties at compile time avoids the runtime reflection
/// entirely and lets Cli.Embedded publish with <c>PublishTrimmed=true</c>.
/// </para>
/// <para>
/// Property naming follows the same PascalCase → snake_case convention that
/// Scriban's default member renamer applies, so existing templates continue to
/// work unchanged. Override per property with <see cref="ScriptNameAttribute"/>;
/// skip a property entirely with <see cref="ScriptIgnoreAttribute"/>.
/// </para>
/// </remarks>
[AttributeUsage(
    AttributeTargets.Class | AttributeTargets.Interface | AttributeTargets.Assembly,
    Inherited = false,
    AllowMultiple = true)]
public sealed class RevelaTemplateModelAttribute : Attribute
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RevelaTemplateModelAttribute"/> class
    /// for class-level usage.
    /// </summary>
    public RevelaTemplateModelAttribute() => TargetType = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="RevelaTemplateModelAttribute"/> class
    /// for assembly-level usage, opting <paramref name="targetType"/> into generation.
    /// </summary>
    /// <param name="targetType">The foreign type to generate a <c>ToScriptObject()</c> extension for.</param>
    public RevelaTemplateModelAttribute(Type targetType) => TargetType = targetType;

    /// <summary>
    /// When used at assembly level, the foreign type to generate extensions for.
    /// <see langword="null"/> for class-level usage.
    /// </summary>
    public Type? TargetType { get; }
}
