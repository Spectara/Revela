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
///   "theme": "Lumina",
///   "themes": { "Spectara.Revela.Themes.Lumina": "2.0.0" },  // overrides global
///   "plugins": { "Spectara.Revela.Plugins.Source.OneDrive": "1.0.0" }  // extends
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class DependenciesConfig
{
    /// <summary>
    /// Configuration section name
    /// </summary>
    public const string SectionName = "dependencies";

    /// <summary>
    /// Active theme name (short name like "Lumina" or full package ID)
    /// </summary>
    /// <remarks>
    /// This determines which theme is used for rendering.
    /// Can be a short name (auto-prefixed with "Spectara.Revela.Themes.")
    /// or a full package ID.
    /// </remarks>
    public string? Theme { get; init; }

    /// <summary>
    /// Installed theme packages with versions
    /// </summary>
    /// <remarks>
    /// Key: Full package ID (e.g., "Spectara.Revela.Themes.Lumina")
    /// Value: Version string (e.g., "1.0.0") or null for latest
    /// </remarks>
    public Dictionary<string, string?> Themes { get; init; } = [];

    /// <summary>
    /// Installed plugin packages with versions
    /// </summary>
    /// <remarks>
    /// Key: Full package ID (e.g., "Spectara.Revela.Plugins.Statistics")
    /// Value: Version string (e.g., "1.0.0") or null for latest
    /// </remarks>
    public Dictionary<string, string?> Plugins { get; init; } = [];
}
