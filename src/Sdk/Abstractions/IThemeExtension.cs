namespace Spectara.Revela.Sdk.Abstractions;

/// <summary>
/// Theme extension plugin interface — extends a theme with plugin-specific templates and assets.
/// </summary>
/// <remarks>
/// <para>
/// Theme extensions provide templates and CSS for specific plugins, styled for a specific theme.
/// </para>
/// <para>
/// Naming convention: Spectara.Revela.Theme.{ThemeName}.{PluginName}
/// Example: Spectara.Revela.Theme.Lumina.Statistics
/// </para>
/// <para>
/// Discovery: Extensions are matched to themes by <see cref="TargetTheme"/> property.
/// Template access: Templates are available as "{PartialPrefix}/{name}" in Scriban.
/// </para>
/// </remarks>
public interface IThemeExtension : IPlugin
{
    /// <summary>
    /// Name of the target theme (e.g., "Lumina").
    /// Matched case-insensitively against IThemePlugin.Metadata.Name.
    /// </summary>
    string TargetTheme { get; }

    /// <summary>
    /// Prefix for partial templates (e.g., "statistics").
    /// Templates are accessed as "{PartialPrefix}/{name}" in Scriban.
    /// </summary>
    string PartialPrefix { get; }

    /// <summary>
    /// Extension variables with default values, merged with theme variables.
    /// </summary>
    IReadOnlyDictionary<string, string> Variables { get; }

    /// <summary>
    /// Get default data sources for a template.
    /// </summary>
    /// <param name="templateKey">Template key relative to extension (e.g., "body/overview").</param>
    /// <returns>Dictionary of variable name → default filename, or empty if no defaults.</returns>
    IReadOnlyDictionary<string, string> GetTemplateDataDefaults(string templateKey);

    /// <summary>
    /// Get a file from the extension as a stream.
    /// </summary>
    /// <param name="relativePath">Relative path within the extension.</param>
    /// <returns>Stream with file contents, or null if not found.</returns>
    Stream? GetFile(string relativePath);

    /// <summary>
    /// Get all file paths in the extension.
    /// </summary>
    IEnumerable<string> GetAllFiles();

    /// <summary>
    /// Extract all extension files to a directory.
    /// </summary>
    Task ExtractToAsync(string targetDirectory, CancellationToken cancellationToken = default);
}
