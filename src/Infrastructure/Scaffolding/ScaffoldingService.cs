using Scriban;

namespace Spectara.Revela.Infrastructure.Scaffolding;

/// <summary>
/// Service for loading and rendering scaffolding templates (project configs and default theme)
/// </summary>
public static class ScaffoldingService
{
    private const string ResourcePrefix = "Spectara.Revela.Infrastructure.Scaffolding.Templates.";

    /// <summary>
    /// Gets raw template content from embedded resources
    /// </summary>
    /// <param name="templatePath">Template path (e.g., "Project.site.json" or "DefaultTheme.layout.html")</param>
    /// <returns>Template content as string</returns>
    public static string GetTemplate(string templatePath)
    {
        var assembly = typeof(ScaffoldingService).Assembly;
        var resourceName = $"{ResourcePrefix}{templatePath.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Template not found: {templatePath} (Resource: {resourceName})");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Renders a template with the given model using Scriban
    /// </summary>
    /// <param name="templatePath">Template path</param>
    /// <param name="model">Data model for template</param>
    /// <returns>Rendered template content</returns>
    public static string RenderTemplate(string templatePath, object model)
    {
        var templateContent = GetTemplate(templatePath);
        var template = Template.Parse(templateContent);
        return template.Render(model);
    }

    /// <summary>
    /// Copies a template file to a destination path (used by init theme command)
    /// </summary>
    /// <param name="templatePath">Template path in embedded resources</param>
    /// <param name="destinationPath">Destination file path</param>
    public static void CopyTemplateTo(string templatePath, string destinationPath)
    {
        var content = GetTemplate(templatePath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(destinationPath, content);
    }

    /// <summary>
    /// Checks if a template exists in embedded resources
    /// </summary>
    /// <param name="templatePath">Template path to check</param>
    /// <returns>True if template exists, false otherwise</returns>
    public static bool TemplateExists(string templatePath)
    {
        var assembly = typeof(ScaffoldingService).Assembly;
        var resourceName = $"{ResourcePrefix}{templatePath.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream != null;
    }
}

