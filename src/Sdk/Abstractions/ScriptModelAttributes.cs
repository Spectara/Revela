namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Skips a property when the Revela <c>ToScriptObject()</c> source generator
/// emits the template-model conversion. Use for properties that should never
/// be visible to templates (internal IDs, cached lookups, etc.).
/// </summary>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ScriptIgnoreAttribute : Attribute;

/// <summary>
/// Overrides the template-visible name of a property when the Revela
/// <c>ToScriptObject()</c> source generator emits the template-model conversion.
/// Default is PascalCase → snake_case conversion of the C# property name.
/// </summary>
/// <param name="name">The exact key to use in the generated <c>ScriptObject</c>.</param>
[AttributeUsage(AttributeTargets.Property, Inherited = false, AllowMultiple = false)]
public sealed class ScriptNameAttribute(string name) : Attribute
{
    /// <summary>The exact key emitted into the <c>ScriptObject</c>.</summary>
    public string Name { get; } = name;
}
