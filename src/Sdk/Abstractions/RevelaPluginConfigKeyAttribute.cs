namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Declares that the annotated plugin or theme assembly owns the configuration
/// node <c>plugins:&lt;key&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// The Revela SDK source generator emits this attribute for every
/// <see cref="RevelaConfigAttribute"/> section of the form <c>plugins:&lt;key&gt;</c>,
/// so plugin authors never write it by hand.
/// </para>
/// <para>
/// The host reads the attribute from each loaded plugin assembly <em>before</em>
/// services are configured. Two packages claiming the same key fail plugin loading;
/// keys under <c>plugins</c> that no loaded package claims produce a warning.
/// Reading assembly attributes is trim/AOT-safe and works for statically referenced
/// and dynamically loaded plugins alike.
/// </para>
/// </remarks>
/// <param name="key">The claimed key below <c>plugins</c> (e.g. <c>serve</c>).</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class RevelaPluginConfigKeyAttribute(string key) : Attribute
{
    /// <summary>The claimed key below <c>plugins</c> (e.g. <c>serve</c>).</summary>
    public string Key { get; } = key;
}
