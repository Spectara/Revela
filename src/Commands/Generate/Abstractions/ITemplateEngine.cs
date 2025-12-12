using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Abstraction for template rendering operations
/// </summary>
/// <remarks>
/// Implementations handle:
/// - Template parsing and compilation
/// - Variable substitution with data models
/// - Custom functions (url_for, format_date, etc.)
/// - Partial/include support via themes
/// </remarks>
public interface ITemplateEngine
{
    /// <summary>
    /// Set the theme for loading partials
    /// </summary>
    /// <param name="theme">Theme plugin to load partials from</param>
    void SetTheme(IThemePlugin? theme);

    /// <summary>
    /// Set the theme extensions for loading extension partials
    /// </summary>
    /// <param name="extensions">Theme extensions that match the current theme</param>
    void SetExtensions(IReadOnlyList<IThemeExtension> extensions);

    /// <summary>
    /// Render template content with data model
    /// </summary>
    /// <param name="templateContent">Template string content</param>
    /// <param name="model">Data model to bind to template</param>
    /// <returns>Rendered HTML output</returns>
    string Render(string templateContent, object model);

    /// <summary>
    /// Render template file with data model
    /// </summary>
    /// <param name="templatePath">Path to template file</param>
    /// <param name="model">Data model to bind to template</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rendered HTML output</returns>
    Task<string> RenderFileAsync(string templatePath, object model, CancellationToken cancellationToken = default);
}
