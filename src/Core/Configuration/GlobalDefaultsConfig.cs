namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Default settings for new projects (from revela.json)
/// </summary>
/// <remarks>
/// <para>
/// Binds to the "defaults" section in revela.json:
/// </para>
/// <code>
/// {
///   "defaults": {
///     "theme": "Lumina"
///   }
/// }
/// </code>
/// </remarks>
public sealed class GlobalDefaultsConfig
{
    /// <summary>
    /// Configuration section name in JSON files
    /// </summary>
    public const string SectionName = "defaults";

    /// <summary>
    /// Default theme for new projects
    /// </summary>
    /// <remarks>
    /// Can be a short name (e.g., "Lumina") or full package ID
    /// (e.g., "Spectara.Revela.Theme.Lumina").
    /// </remarks>
    public string Theme { get; init; } = "Lumina";
}
