namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Marks a configuration class as a Revela options type. The attribute is a
/// documentation marker only — there is no source generator behind it.
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
/// <code>
/// [RevelaConfig("Spectara.Revela.Plugins.MyPlugin")]
/// internal sealed class MyPluginConfig
/// {
///     public const string Section = "Spectara.Revela.Plugins.MyPlugin";
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
/// <param name="sectionName">The configuration section name (e.g., "project" or "Spectara.Revela.Plugins.MyPlugin").</param>
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
