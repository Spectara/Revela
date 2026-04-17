namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Marks a configuration class for automatic <c>IOptions&lt;T&gt;</c> registration.
/// </summary>
/// <remarks>
/// <para>
/// The Revela Source Generator creates an extension method to register this config:
/// <c>services.Add{ClassName}()</c> which calls <c>AddOptions&lt;T&gt;().BindConfiguration(sectionName)</c>.
/// </para>
/// <para>
/// Example:
/// </para>
/// <code>
/// [RevelaConfig("Spectara.Revela.Plugins.MyPlugin")]
/// public sealed class MyPluginConfig
/// {
///     [Required] public string ApiUrl { get; init; } = string.Empty;
///     public int Timeout { get; init; } = 30;
/// }
///
/// // Generated extension method (auto-generated, in same assembly):
/// // services.AddMyPluginConfig();
/// </code>
/// </remarks>
/// <param name="sectionName">The configuration section name (e.g., "project" or "Spectara.Revela.Plugins.MyPlugin").</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class RevelaConfigAttribute(string sectionName) : Attribute
{
    /// <summary>
    /// The configuration section name.
    /// </summary>
    public string SectionName { get; } = sectionName;

    /// <summary>
    /// Whether to call <c>ValidateDataAnnotations()</c>. Default: <c>true</c>.
    /// </summary>
    public bool ValidateDataAnnotations { get; init; } = true;

    /// <summary>
    /// Whether to call <c>ValidateOnStart()</c>. Default: <c>true</c>.
    /// </summary>
    public bool ValidateOnStart { get; init; } = true;
}
