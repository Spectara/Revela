using System.Globalization;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Spectara.Revela.Features.Generate.Abstractions;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Template engine using Scriban (Liquid-like syntax)
/// </summary>
/// <remarks>
/// Scriban features:
/// - Liquid-compatible syntax (familiar to web developers)
/// - Fast compilation and rendering
/// - Sandboxed execution (safe for user templates)
/// - Partials and layouts support via include directive
/// - Custom functions
///
/// Example template:
/// <code>
/// &lt;h1&gt;{{ site.title }}&lt;/h1&gt;
/// {{ include 'partials/navigation' nav_items }}
/// {{ for image in images }}
///   &lt;img src="{{ image.url }}" alt="{{ image.title }}" /&gt;
/// {{ end }}
/// </code>
/// </remarks>
public sealed partial class ScribanTemplateEngine(ILogger<ScribanTemplateEngine> logger) : ITemplateEngine
{
    private readonly Dictionary<string, Template> compiledTemplates = [];
    private string? themeDirectory;

    /// <summary>
    /// Set the theme directory for loading partials
    /// </summary>
    /// <param name="directory">Absolute path to the theme directory</param>
    public void SetThemeDirectory(string directory)
    {
        themeDirectory = directory;
        LogThemeDirectorySet(logger, directory);
    }

    /// <summary>
    /// Render template content with data model
    /// </summary>
    public string Render(string templateContent, object model)
    {
        try
        {
            // Parse and compile template (cached internally by Scriban)
            var template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing failed: {errors}");
            }

            // Create script context with custom functions and template loader
            var context = CreateScriptContext(model, themeDirectory);

            // Render template
            var result = template.Render(context);

            return result;
        }
        catch (Exception ex)
        {
            LogRenderingFailed(logger, ex);
            throw;
        }
    }

    /// <summary>
    /// Render template file with data model
    /// </summary>
    public async Task<string> RenderFileAsync(string templatePath, object model, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Template not found: {templatePath}", templatePath);
        }

        LogRenderingTemplate(logger, templatePath);

        // Read template content
        var content = await File.ReadAllTextAsync(templatePath, cancellationToken);

        // Render with caching
        return RenderWithCache(templatePath, content, model);
    }

    /// <summary>
    /// Render template with compilation caching
    /// </summary>
    private string RenderWithCache(string templateKey, string templateContent, object model)
    {
        // Check if template is already compiled
        if (!compiledTemplates.TryGetValue(templateKey, out var template))
        {
            // Parse and compile
            template = Template.Parse(templateContent);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing failed: {errors}");
            }

            // Cache compiled template
            compiledTemplates[templateKey] = template;
        }

        // Create context and render
        var context = CreateScriptContext(model, themeDirectory);
        return template.Render(context);
    }

    /// <summary>
    /// Create Scriban context with custom functions and template loader
    /// </summary>
    private TemplateContext CreateScriptContext(object model, string? themePath)
    {
        var context = new TemplateContext();

        // Set up template loader for partials if theme directory is set
        if (!string.IsNullOrEmpty(themePath))
        {
            context.TemplateLoader = new ThemeTemplateLoader(themePath, compiledTemplates, logger);
        }

        var scriptObject = new ScriptObject();

        // Import model as global variables
        scriptObject.Import(model, renamer: member => member.Name);

        // Register custom functions
        scriptObject.Import("url_for", new Func<string, string>(UrlFor));
        scriptObject.Import("asset_url", new Func<string, string>(AssetUrl));
        scriptObject.Import("image_url", new Func<string, int, string, string>(ImageUrl));
        scriptObject.Import("format_date", new Func<DateTime, string, string>(FormatDate));
        scriptObject.Import("format_filesize", new Func<long, string>(FormatFileSize));
        scriptObject.Import("format_exif_exposure", new Func<double?, string>(FormatExifExposure));
        scriptObject.Import("format_exif_aperture", new Func<double?, string>(FormatExifAperture));

        context.PushGlobal(scriptObject);

        return context;
    }

    // Custom template functions

    /// <summary>
    /// Generate URL for a page/gallery
    /// </summary>
    /// <example>{{ url_for "gallery/vacation" }} → /gallery/vacation/index.html</example>
    private static string UrlFor(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/";
        }

        // Normalize path
        path = path.Trim('/').Replace('\\', '/');

        // Add index.html
        return $"/{path}/index.html";
    }

    /// <summary>
    /// Generate URL for static asset (CSS, JS)
    /// </summary>
    /// <example>{{ asset_url "css/style.css" }} → /assets/css/style.css</example>
    private static string AssetUrl(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return "/assets/";
        }

        path = path.Trim('/').Replace('\\', '/');
        return $"/assets/{path}";
    }

    /// <summary>
    /// Generate URL for image variant (specific size and format)
    /// </summary>
    /// <example>{{ image_url "photo.jpg" 1920 "webp" }} → /images/photo-1920w.webp</example>
    private static string ImageUrl(string fileName, int width, string format)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return "/images/";
        }

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(fileName);
        return $"/images/{nameWithoutExtension}-{width}w.{format}";
    }

    /// <summary>
    /// Format date with custom format string
    /// </summary>
    /// <example>{{ format_date image.date_taken "yyyy-MM-dd" }} → 2024-01-20</example>
    private static string FormatDate(DateTime date, string format)
    {
        try
        {
            return date.ToString(format, CultureInfo.InvariantCulture);
        }
        catch
        {
            return date.ToShortDateString();
        }
    }

    /// <summary>
    /// Format file size in human-readable format
    /// </summary>
    /// <example>{{ format_filesize 1048576 }} → 1.00 MB</example>
    private static string FormatFileSize(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return $"{len:0.##} {sizes[order]}";
    }

    /// <summary>
    /// Format EXIF exposure time (e.g., "1/500s")
    /// </summary>
    private static string FormatExifExposure(double? exposureTime)
    {
        if (!exposureTime.HasValue)
        {
            return "N/A";
        }

        var value = exposureTime.Value;

        // If less than 1 second, show as fraction
        if (value < 1)
        {
            var denominator = (int)(1 / value);
            return $"1/{denominator}s";
        }

        return $"{value:0.##}s";
    }

    /// <summary>
    /// Format EXIF aperture (e.g., "f/2.8")
    /// </summary>
    private static string FormatExifAperture(double? fNumber)
    {
        if (!fNumber.HasValue)
        {
            return "N/A";
        }

        return $"f/{fNumber.Value:0.#}";
    }

    /// <summary>
    /// Clear compiled template cache
    /// </summary>
    public void ClearCache()
    {
        compiledTemplates.Clear();
        LogCacheCleared(logger);
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Error, Message = "Template rendering failed")]
    static partial void LogRenderingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rendering template: {Path}")]
    static partial void LogRenderingTemplate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Template cache cleared")]
    static partial void LogCacheCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme directory set: {Directory}")]
    static partial void LogThemeDirectorySet(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading partial template: {PartialName}")]
    static partial void LogLoadingPartial(ILogger logger, string partialName);
}

/// <summary>
/// Template loader for loading partials from the theme directory
/// </summary>
/// <remarks>
/// Supports the Scriban include directive:
/// <code>
/// {{ include 'partials/navigation' nav_items }}
/// </code>
///
/// Searches for templates in the theme directory with .html extension.
/// </remarks>
internal sealed partial class ThemeTemplateLoader(
    string themeDirectory,
    Dictionary<string, Template> templateCache,
    ILogger logger) : ITemplateLoader
{
    /// <summary>
    /// Get the path for a template include
    /// </summary>
    /// <param name="context">Template context</param>
    /// <param name="callerSpan">Source span of the include directive</param>
    /// <param name="templateName">Name of the template to include (e.g., "partials/navigation")</param>
    /// <returns>Absolute path to the template file</returns>
    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        // Add .html extension if not present
        var fileName = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : templateName + ".html";

        // Combine with theme directory
        var fullPath = Path.Combine(themeDirectory, fileName);

        return Path.GetFullPath(fullPath);
    }

    /// <summary>
    /// Load template content from file
    /// </summary>
    /// <param name="context">Template context</param>
    /// <param name="callerSpan">Source span of the include directive</param>
    /// <param name="templatePath">Full path to the template file</param>
    /// <returns>Template content as string</returns>
    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        LogLoadingPartial(logger, templatePath);

        if (!File.Exists(templatePath))
        {
            throw new FileNotFoundException($"Partial template not found: {templatePath}", templatePath);
        }

        // Check cache first
        if (templateCache.TryGetValue(templatePath, out _))
        {
            // Template already compiled, just return content for re-parsing
            // (Scriban handles caching internally when using ITemplateLoader)
        }

        var content = File.ReadAllText(templatePath);

        // Parse and cache the template
        var template = Template.Parse(content);
        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Partial template parsing failed ({templatePath}): {errors}");
        }

        templateCache[templatePath] = template;

        return content;
    }

    /// <summary>
    /// Async version of Load (required by interface)
    /// </summary>
    public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        return new ValueTask<string>(Load(context, callerSpan, templatePath));
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading partial template: {PartialPath}")]
    static partial void LogLoadingPartial(ILogger logger, string partialPath);
}
