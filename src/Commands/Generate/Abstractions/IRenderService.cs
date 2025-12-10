using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Commands.Generate.Abstractions;

/// <summary>
/// Service for template rendering and HTML page generation.
/// </summary>
/// <remarks>
/// <para>
/// Renders HTML pages from manifest data:
/// </para>
/// <list type="bullet">
///   <item><description>Load and render templates (Scriban)</description></item>
///   <item><description>Generate index, gallery, and image pages</description></item>
///   <item><description>Copy theme assets to output</description></item>
/// </list>
/// </remarks>
public interface IRenderService
{
    /// <summary>
    /// Render HTML pages from manifest data.
    /// </summary>
    /// <param name="progress">Optional progress reporter.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Render result with statistics.</returns>
    Task<RenderResult> RenderAsync(
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Set the theme for template loading and rendering.
    /// </summary>
    /// <param name="theme">Theme plugin to use for templates and assets.</param>
    void SetTheme(IThemePlugin? theme);

    /// <summary>
    /// Render template content with data model.
    /// </summary>
    /// <param name="templateContent">Template source code.</param>
    /// <param name="model">Data model for template.</param>
    /// <returns>Rendered output.</returns>
    string Render(string templateContent, object model);

    /// <summary>
    /// Render template file with data model.
    /// </summary>
    /// <param name="templatePath">Path to template file.</param>
    /// <param name="model">Data model for template.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Rendered output.</returns>
    Task<string> RenderFileAsync(
        string templatePath,
        object model,
        CancellationToken cancellationToken = default);
}
