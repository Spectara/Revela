namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Marker interface for plugin configuration models.
/// </summary>
/// <remarks>
/// <para>
/// Implement this interface on your plugin configuration class to enable
/// the <c>AddPluginConfig&lt;T&gt;()</c> extension method, which automates
/// configuration binding and validation.
/// </para>
/// <para>
/// The <see cref="SectionName"/> property provides the configuration section key
/// used to bind settings from project.json and environment variables.
/// Convention: use the full package ID (e.g., "Spectara.Revela.Plugins.MyPlugin").
/// </para>
/// <example>
/// <code>
/// internal sealed class MyPluginConfig : IPluginConfig
/// {
///     public static string SectionName => "Spectara.Revela.Plugins.MyPlugin";
///
///     [Range(1, 100)]
///     public int MaxItems { get; init; } = 10;
/// }
/// </code>
/// </example>
/// </remarks>
public interface IPluginConfig
{
    /// <summary>
    /// Gets the configuration section name for binding.
    /// </summary>
    /// <remarks>
    /// Convention: Use the full NuGet package ID as section name.
    /// This maps directly to the root key in project.json and
    /// environment variable prefix (with '__' separators).
    /// </remarks>
    static abstract string SectionName { get; }
}
