using Spectara.Revela.Sdk.Abstractions;

namespace Spectara.Revela.Sdk.Configuration;

/// <summary>
/// Configuration for theme and plugin dependencies
/// </summary>
/// <remarks>
/// <para>
/// This configuration is merged from multiple sources (in order, later wins):
/// </para>
/// <list type="number">
/// <item><b>revela.json</b> (global): User-wide default themes/plugins</item>
/// <item><b>project.json</b> (local): Project-specific themes/plugins</item>
/// </list>
/// <para>
/// The .NET Configuration system automatically merges these sources.
/// Local settings override global settings for the same key.
/// </para>
/// <example>
/// <code>
/// // revela.json (global)
/// {
///   "themes": { "Spectara.Revela.Themes.Lumina": "1.0.0" },
///   "plugins": { "Spectara.Revela.Plugins.Statistics": "1.0.0" }
/// }
///
/// // project.json (local)
/// {
///   "theme": { "name": "Lumina" },
///   "themes": { "Spectara.Revela.Themes.Lumina": "2.0.0" },  // overrides global
///   "plugins": { "Spectara.Revela.Plugins.Source.OneDrive": "1.0.0" }  // extends
/// }
/// </code>
/// </example>
/// </remarks>
[RevelaConfig("", ValidateDataAnnotations = false)]
public sealed class DependenciesConfig
{
    /// <summary>
    /// Empty section name binds the root-level themes and plugins maps.
    /// Matches the <c>[RevelaConfig]</c> attribute argument; passed to
    /// <c>BindConfiguration</c> at registration time.
    /// </summary>
    public const string Section = "";

    /// <summary>
    /// Installed theme packages with versions
    /// </summary>
    /// <remarks>
    /// Key: Full package ID (e.g., "Spectara.Revela.Themes.Lumina")
    /// Value: Version string (e.g., "1.0.0") or null for latest
    /// </remarks>
    public Dictionary<string, string?> Themes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Installed plugin packages with versions
    /// </summary>
    /// <remarks>
    /// Key: Full package ID (e.g., "Spectara.Revela.Plugins.Statistics")
    /// Value: Version string (e.g., "1.0.0") or null for latest
    /// </remarks>
    public Dictionary<string, string?> Plugins { get; } = [];
}
