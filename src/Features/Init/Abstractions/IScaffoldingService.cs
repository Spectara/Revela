namespace Spectara.Revela.Features.Init.Abstractions;

/// <summary>
/// Service for loading and rendering scaffolding templates (project configs and default theme).
/// </summary>
public interface IScaffoldingService
{
    /// <summary>
    /// Gets raw template content from embedded resources.
    /// </summary>
    /// <param name="templatePath">Template path (e.g., "Project.site.json" or "Theme.layout.html").</param>
    /// <returns>Template content as string.</returns>
    string GetTemplate(string templatePath);

    /// <summary>
    /// Renders a template with the given model using Scriban.
    /// </summary>
    /// <param name="templatePath">Template path.</param>
    /// <param name="model">Data model for template.</param>
    /// <returns>Rendered template content.</returns>
    string RenderTemplate(string templatePath, object model);

    /// <summary>
    /// Copies a template file to a destination path (used by init theme command).
    /// </summary>
    /// <param name="templatePath">Template path in embedded resources.</param>
    /// <param name="destinationPath">Destination file path.</param>
    void CopyTemplateTo(string templatePath, string destinationPath);

    /// <summary>
    /// Checks if a template exists in embedded resources.
    /// </summary>
    /// <param name="templatePath">Template path to check.</param>
    /// <returns>True if template exists, false otherwise.</returns>
    bool TemplateExists(string templatePath);
}
