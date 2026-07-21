namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Assembly-level opt-in that tells the config-keys source generator to emit a
/// <c>&lt;Poco&gt;Keys</c> constants class (and the classes for its nested config
/// POCOs) into the annotated assembly.
/// </summary>
/// <remarks>
/// <para>
/// Config POCOs marked with <see cref="RevelaConfigAttribute"/> get their keys
/// emitted automatically in the assembly that declares them. Assemblies that write
/// config for POCOs declared elsewhere (e.g. a command in the host writing the SDK's
/// <c>ProjectConfig</c>) opt in with this attribute so the constants are generated
/// locally:
/// </para>
/// <code>
/// [assembly: RevelaConfigKeys(typeof(ProjectConfig))]
/// </code>
/// <para>
/// The keys are emitted per-assembly (as <c>internal</c>) rather than once in the
/// SDK because same-assembly generated types are the only ones tooling such as
/// <c>dotnet format</c> resolves reliably. Naming the config type explicitly in
/// source (instead of scanning referenced metadata) keeps the generator working
/// under those design-time builds.
/// </para>
/// </remarks>
/// <param name="configType">The configuration POCO to emit a keys class for.</param>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true, Inherited = false)]
public sealed class RevelaConfigKeysAttribute(Type configType) : Attribute
{
    /// <summary>The configuration POCO whose keys should be generated.</summary>
    public Type ConfigType { get; } = configType;
}
