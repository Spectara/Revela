namespace Spectara.Revela.Core.Configuration;

/// <summary>
/// Theme configuration settings.
/// </summary>
/// <remarks>
/// <para>
/// Loaded from the "theme" section of project.json.
/// Contains theme selection and theme-specific options.
/// </para>
/// <example>
/// <code>
/// // project.json
/// {
///   "theme": {
///     "name": "Lumina"
///   }
/// }
/// </code>
/// </example>
/// </remarks>
public sealed class ThemeConfig
{
    /// <summary>
    /// Configuration section name.
    /// </summary>
    public const string SectionName = "theme";

    /// <summary>
    /// Name of the theme to use (e.g., "Lumina").
    /// </summary>
    public string Name { get; init; } = string.Empty;
}
