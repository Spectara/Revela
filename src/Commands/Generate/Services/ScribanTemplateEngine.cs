using System.Globalization;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Core.Abstractions;

namespace Spectara.Revela.Commands.Generate.Services;

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
    private IThemePlugin? currentTheme;
    private IReadOnlyList<IThemeExtension> themeExtensions = [];

    /// <summary>
    /// Converts PascalCase member names to lowercase for Scriban templates
    /// </summary>
    /// <remarks>
    /// Scriban templates use lowercase property names ({{ site.title }})
    /// but C# properties are PascalCase (Site.Title). This renamer bridges the gap.
    ///
    /// CA1308 is suppressed because this is intentional case conversion for template
    /// compatibility, not string normalization for comparison. Scriban requires lowercase.
    /// </remarks>
    /// <summary>
    /// Convert PascalCase property names to snake_case for Scriban templates.
    /// Example: Sizes → sizes
    /// </summary>
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Scriban templates require lowercase property names - this is format conversion, not normalization")]
    private static string ConvertToSnakeCase(System.Reflection.MemberInfo member)
    {
        var name = member.Name;
        var result = new System.Text.StringBuilder(name.Length + 5);

        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (char.IsUpper(c))
            {
                if (i > 0)
                {
                    result.Append('_');
                }

                result.Append(char.ToLowerInvariant(c));
            }
            else
            {
                result.Append(c);
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Set the theme for loading partials
    /// </summary>
    /// <param name="theme">Theme plugin to load partials from</param>
    public void SetTheme(IThemePlugin? theme)
    {
        currentTheme = theme;
        if (theme is not null)
        {
            LogThemeSet(logger, theme.Metadata.Name);
        }
    }

    /// <summary>
    /// Set theme extensions for loading plugin-specific partials
    /// </summary>
    /// <param name="extensions">Theme extensions to use for partial lookups</param>
    public void SetExtensions(IReadOnlyList<IThemeExtension> extensions)
    {
        themeExtensions = extensions;
        foreach (var ext in extensions)
        {
            LogExtensionSet(logger, ext.Metadata.Name, ext.PartialPrefix);
        }
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
            var context = CreateScriptContext(model);

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
        var context = CreateScriptContext(model);
        return template.Render(context);
    }

    /// <summary>
    /// Create Scriban context with custom functions and template loader
    /// </summary>
    private TemplateContext CreateScriptContext(object model)
    {
        var context = new TemplateContext
        {
            // Use Scriban's built-in member renamer for snake_case property names
            // This is required because templates use snake_case ({{ site.build_date }})
            // but C# properties are PascalCase (Site.BuildDate)
            MemberRenamer = ConvertToSnakeCase,

            // Disable loop limit (default 1000) - our templates are trusted, not user-provided
            // Large galleries with nested loops (images × formats × sizes) easily exceed 1000
            // See: https://github.com/scriban/scriban/blob/master/doc/runtime.md#safe-runtime
            LoopLimit = 0
        };

        // Set up template loader for partials if theme is set
        if (currentTheme is not null)
        {
            context.TemplateLoader = new ThemePluginTemplateLoader(
                currentTheme, themeExtensions, compiledTemplates, logger);
        }

        var scriptObject = new ScriptObject();

        // Import model as global variables
        // MemberRenamer handles the PascalCase → lowercase conversion
        scriptObject.Import(model);

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
    /// Format EXIF exposure time (e.g., "1/500s", "0.5s", "10s")
    /// </summary>
    /// <remarks>
    /// Formatting rules:
    /// - Less than 0.3s: Show as fraction (1/500s, 1/60s, 1/4s)
    /// - 0.3s to 0.9s: Show as decimal (0.4s, 0.5s, 0.8s)
    /// - 1s and longer: Show as whole seconds (1s, 10s, 30s)
    /// </remarks>
    private static string FormatExifExposure(double? exposureTime)
    {
        if (!exposureTime.HasValue)
        {
            return "N/A";
        }

        var value = exposureTime.Value;

        // 1 second or longer: show as whole/decimal seconds
        if (value >= 1)
        {
            // If it's a whole number, don't show decimals
            if (Math.Abs(value - Math.Round(value)) < 0.001)
            {
                return $"{(int)value}s";
            }

            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}s", value);
        }

        // 0.3s to 0.9s: show as decimal
        if (value >= 0.3)
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.#}s", value);
        }

        // Less than 0.3s: show as fraction 1/X
        var denominator = (int)Math.Round(1 / value);
        return $"1/{denominator}s";
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
    private static partial void LogRenderingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rendering template: {Path}")]
    private static partial void LogRenderingTemplate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Template cache cleared")]
    private static partial void LogCacheCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme set for template engine: {ThemeName}")]
    private static partial void LogThemeSet(ILogger logger, string themeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme extension set: {ExtensionName} (prefix: {Prefix})")]
    private static partial void LogExtensionSet(ILogger logger, string extensionName, string prefix);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading partial template: {PartialName}")]
    private static partial void LogLoadingPartial(ILogger logger, string partialName);
}

/// <summary>
/// Template loader for loading partials from IThemePlugin and IThemeExtensions
/// </summary>
/// <remarks>
/// <para>
/// Supports the Scriban include directive:
/// <code>
/// {{ include 'partials/navigation' nav_items }}
/// {{ include 'statistics/chart' stats }}
/// </code>
/// </para>
/// <para>
/// Resolution order:
/// 1. Theme partials (e.g., "partials/navigation" → theme's partials/navigation.html)
/// 2. Extension partials (e.g., "statistics/chart" → extension with prefix "statistics")
/// </para>
/// <para>
/// If an extension partial is not found, a helpful warning is shown with installation hints.
/// </para>
/// </remarks>
internal sealed partial class ThemePluginTemplateLoader(
    IThemePlugin theme,
    IReadOnlyList<IThemeExtension> extensions,
    Dictionary<string, Template> templateCache,
    ILogger logger) : ITemplateLoader
{
    /// <summary>
    /// Get the path for a template include
    /// </summary>
    /// <param name="context">Template context</param>
    /// <param name="callerSpan">Source span of the include directive</param>
    /// <param name="templateName">Name of the template to include (e.g., "partials/navigation")</param>
    /// <returns>Template path (used as cache key)</returns>
    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        // Add .html extension if not present
        var fileName = templateName.EndsWith(".html", StringComparison.OrdinalIgnoreCase)
            ? templateName
            : templateName + ".html";

        // Return as cache key (theme name + path for uniqueness)
        return $"{theme.Metadata.Name}:{fileName}";
    }

    /// <summary>
    /// Load template content from theme or extensions
    /// </summary>
    /// <param name="context">Template context</param>
    /// <param name="callerSpan">Source span of the include directive</param>
    /// <param name="templatePath">Template path (cache key from GetPath)</param>
    /// <returns>Template content as string</returns>
    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        // Extract actual file path from cache key
        var colonIndex = templatePath.IndexOf(':', StringComparison.Ordinal);
        var filePath = colonIndex >= 0 ? templatePath[(colonIndex + 1)..] : templatePath;

        // 1. Try to load from theme (check manifest for partial mapping)
        var resolvedPath = ResolveThemePartialPath(filePath);
        var stream = theme.GetFile(resolvedPath);
        if (stream is not null)
        {
            LogLoadingPartial(logger, filePath, theme.Metadata.Name);
            return LoadFromStream(stream, filePath, templatePath);
        }

        // 2. Try to load from extensions (check prefix match)
        var slashIndex = filePath.IndexOf('/', StringComparison.Ordinal);
        if (slashIndex > 0)
        {
            var prefix = filePath[..slashIndex];
            var templateFileName = filePath[(slashIndex + 1)..];

            // Find extension with matching prefix
            var extension = extensions.FirstOrDefault(e =>
                e.PartialPrefix.Equals(prefix, StringComparison.OrdinalIgnoreCase));

            if (extension is not null)
            {
                // Look up the actual file path from extension manifest
                var manifest = extension.GetManifest();
                var templateKey = Path.GetFileNameWithoutExtension(templateFileName);

                if (manifest.Partials.TryGetValue(templateKey, out var extensionFilePath))
                {
                    stream = extension.GetFile(extensionFilePath);
                    if (stream is not null)
                    {
                        LogLoadingExtensionPartial(logger, filePath, extension.Metadata.Name);
                        return LoadFromStream(stream, filePath, templatePath);
                    }
                }

                // Extension exists but template not found in manifest
                LogExtensionTemplateNotFound(logger, templateFileName, extension.Metadata.Name);
            }
            else
            {
                // No extension with this prefix - show helpful warning
                LogExtensionNotInstalled(logger, prefix, theme.Metadata.Name, filePath);
            }
        }

        // 3. Not found anywhere
        throw new FileNotFoundException(
            $"Partial template '{filePath}' not found in theme '{theme.Metadata.Name}' or any extensions. " +
            $"If this is a plugin template, install the theme extension package.",
            filePath);
    }

    private string LoadFromStream(Stream stream, string filePath, string templatePath)
    {
        using (stream)
        using (var reader = new StreamReader(stream))
        {
            var content = reader.ReadToEnd();

            // Parse and cache the template
            var template = Template.Parse(content);
            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Partial template parsing failed ({filePath}): {errors}");
            }

            templateCache[templatePath] = template;

            return content;
        }
    }

    /// <summary>
    /// Resolves a template include path to the actual file path using the theme manifest.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Templates use logical paths like 'partials/navigation' in include directives,
    /// but the actual embedded resource might be at 'Templates/Partials/navigation.html'.
    /// </para>
    /// <para>
    /// This method checks the theme manifest to find the mapping:
    /// - 'partials/navigation.html' → looks up 'navigation' in manifest.Partials
    /// - Returns manifest path like 'Templates/Partials/navigation.html'
    /// - If not found in manifest, returns original path (direct file access)
    /// </para>
    /// </remarks>
    private string ResolveThemePartialPath(string includePath)
    {
        // Check if this is a partials include: 'partials/filename.html'
        if (!includePath.StartsWith("partials/", StringComparison.OrdinalIgnoreCase))
        {
            return includePath;
        }

        // Extract the partial name: 'partials/navigation.html' → 'navigation'
        var partialFileName = includePath["partials/".Length..];
        var partialName = Path.GetFileNameWithoutExtension(partialFileName);

        // Look up in theme manifest
        var manifest = theme.GetManifest();
        if (manifest.Partials.TryGetValue(partialName, out var actualPath))
        {
            LogPartialPathResolved(logger, includePath, actualPath);
            return actualPath;
        }

        // Not in manifest - return original (might be a direct file path)
        return includePath;
    }

    /// <summary>
    /// Async version of Load (required by interface)
    /// </summary>
    public ValueTask<string> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath) =>
        new(Load(context, callerSpan, templatePath));

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading partial template: {PartialPath} from theme {ThemeName}")]
    private static partial void LogLoadingPartial(ILogger logger, string partialPath, string themeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading partial template: {PartialPath} from extension {ExtensionName}")]
    private static partial void LogLoadingExtensionPartial(ILogger logger, string partialPath, string extensionName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Resolved partial path: '{IncludePath}' → '{ActualPath}'")]
    private static partial void LogPartialPathResolved(ILogger logger, string includePath, string actualPath);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Template '{TemplateName}' not found in extension '{ExtensionName}'")]
    private static partial void LogExtensionTemplateNotFound(ILogger logger, string templateName, string extensionName);

    [LoggerMessage(Level = LogLevel.Warning,
        Message = "No theme extension with prefix '{Prefix}' installed for theme '{ThemeName}'. " +
                  "Template '{TemplatePath}' requires a theme extension. " +
                  "Install: dotnet add package Spectara.Revela.Theme.{ThemeName}.{Prefix}")]
    private static partial void LogExtensionNotInstalled(ILogger logger, string prefix, string themeName, string templatePath);
}
