namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Marks a configuration class as a Revela options type and declares its
/// configuration section.
/// </summary>
/// <remarks>
/// <para>
/// Plugin/SDK authors add a <c>public const string Section = "..."</c> on the
/// class with the same value as the attribute argument and use it when
/// registering the options. The hand-written const is required because the
/// .NET Configuration Binding Source Generator only intercepts call sites
/// where the section argument is statically resolvable from user-written
/// source. Constants emitted from another source generator are invisible
/// to CBSG, which would silently fall back to the reflection binder and
/// break under <c>PublishTrimmed</c> / <c>PublishAot</c>.
/// </para>
/// <para>
/// Plugins and themes (MSBuild <c>PackageType</c> <c>RevelaPlugin</c> /
/// <c>RevelaTheme</c>) must use a section of the form <c>plugins:&lt;key&gt;</c>
/// where the key matches <c>^[a-z][a-zA-Z0-9]*$</c> (see <see cref="Configuration.PluginConfigSection"/>).
/// The SDK source generator reports an error for any other section and when the
/// attribute argument and the <c>Section</c> const differ. It also emits a
/// <see cref="RevelaPluginConfigKeyAttribute"/> so the host knows which package
/// owns which key.
/// </para>
/// <code>
/// [RevelaConfig("plugins:myPlugin")]
/// internal sealed class MyPluginConfig
/// {
///     public const string Section = "plugins:myPlugin";
///
///     [Required] public string ApiUrl { get; set; } = string.Empty;
///     public int Timeout { get; set; } = 30;
/// }
///
/// // In IPlugin.ConfigureServices:
/// services.AddOptions&lt;MyPluginConfig&gt;()
///     .BindConfiguration(MyPluginConfig.Section);
/// services.AddSingleton&lt;IValidateOptions&lt;MyPluginConfig&gt;,
///     MyPluginConfigValidator&gt;();   // trim/AOT-safe DataAnnotations
/// </code>
/// </remarks>
/// <param name="sectionName">The configuration section name (e.g., "project" or "plugins:myPlugin").</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RevelaConfigAttribute(string sectionName) : Attribute
{
    /// <summary>
    /// The configuration section name. Mirror this value in a hand-written
    /// <c>public const string Section</c> on the annotated class for use with
    /// <c>BindConfiguration</c>.
    /// </summary>
    public string SectionName { get; } = sectionName;

    /// <summary>
    /// Whether to call <c>ValidateDataAnnotations()</c>. Default: <c>true</c>.
    /// </summary>
    /// <remarks>
    /// Validation runs lazily on first <c>IOptions&lt;T&gt;.Value</c> access — not at startup,
    /// because most config values are produced at runtime (wizards, CLI args, generated sections).
    /// </remarks>
    public bool ValidateDataAnnotations { get; init; } = true;
}
