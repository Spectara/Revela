using Scriban;
using Spectara.Revela.Features.Init.Abstractions;

namespace Spectara.Revela.Features.Init.Services;

/// <summary>
/// Service for loading and rendering scaffolding templates (project configs and default theme).
/// </summary>
public sealed class ScaffoldingService : IScaffoldingService
{
    private const string ResourcePrefix = "Spectara.Revela.Features.Init.Templates.";

    /// <inheritdoc />
    public string GetTemplate(string templatePath)
    {
        var assembly = typeof(ScaffoldingService).Assembly;
        var resourceName = $"{ResourcePrefix}{templatePath.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Template not found: {templatePath} (Resource: {resourceName})");

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <inheritdoc />
    public string RenderTemplate(string templatePath, object model)
    {
        var templateContent = GetTemplate(templatePath);
        var template = Template.Parse(templateContent);
        return template.Render(model);
    }

    /// <inheritdoc />
    public void CopyTemplateTo(string templatePath, string destinationPath)
    {
        var content = GetTemplate(templatePath);

        var directory = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(destinationPath, content);
    }

    /// <inheritdoc />
    public bool TemplateExists(string templatePath)
    {
        var assembly = typeof(ScaffoldingService).Assembly;
        var resourceName = $"{ResourcePrefix}{templatePath.Replace('/', '.')}";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        return stream != null;
    }
}
