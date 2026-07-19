using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Services;

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
/// {{ include 'navigation' nav_items }}
/// {{ for image in images }}
///   &lt;img src="{{ variant_url(image, 640, 'jpg') }}" alt="{{ image.title }}" /&gt;
/// {{ end }}
/// </code>
/// </remarks>
internal sealed partial class ScribanTemplateEngine(
    ILogger<ScribanTemplateEngine> logger,
    IMarkdownService markdownService,
    ITemplateResolver templateResolver) : ITemplateEngine
{
    private readonly ConcurrentDictionary<string, Template> compiledTemplates = new();
    private ITheme? currentTheme;
    private IReadOnlyDictionary<string, Image>? imageLookup;

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
    [SuppressMessage(
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
    public void SetTheme(ITheme? theme)
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
    /// <remarks>
    /// Extensions are now managed by ITemplateResolver. This method is kept for interface compatibility.
    /// </remarks>
    public void SetExtensions(IReadOnlyList<ITheme> extensions)
    {
        foreach (var ext in extensions)
        {
            LogExtensionSet(logger, ext.Metadata.Name, ext.Prefix ?? "(base)");
        }
    }

    /// <summary>
    /// Set the image lookup for the <c>image</c> template function.
    /// </summary>
    public void SetImageLookup(IReadOnlyDictionary<string, Image> imagesBySourcePath) =>
        imageLookup = imagesBySourcePath;

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
        var template = compiledTemplates.GetOrAdd(templateKey, key =>
        {
            var parsed = Template.Parse(templateContent);
            if (parsed.HasErrors)
            {
                var errors = string.Join(", ", parsed.Messages.Select(m => m.Message));
                throw new InvalidOperationException($"Template parsing failed: {errors}");
            }

            return parsed;
        });

        // Create context and render
        var context = CreateScriptContext(model);
        return template.Render(context);
    }

    /// <summary>
    /// Render body content as a Scriban template with optional data.
    /// </summary>
    /// <remarks>
    /// Used for processing _index.md body content that contains Scriban includes.
    /// The data is available as 'stats' variable in the template.
    /// </remarks>
    public string RenderBodyTemplate(string bodyContent, object? stats)
    {
        try
        {
            var template = Template.Parse(bodyContent);

            if (template.HasErrors)
            {
                var errors = string.Join(", ", template.Messages.Select(m => m.Message));
                LogBodyTemplateParsingFailed(logger, errors);
                return bodyContent; // Return original content on parse error
            }

            // Create minimal context with stats data
            var context = new TemplateContext
            {
                MemberRenamer = ConvertToSnakeCase
            };

            // Set up template loader for includes
            if (currentTheme is not null)
            {
                context.TemplateLoader = new TemplateResolverLoader(templateResolver, compiledTemplates, logger);
            }

            // Add stats as a named variable (convert JsonElement to Scriban-compatible types)
            var scriptObject = new ScriptObject();
            if (stats is not null)
            {
                scriptObject["stats"] = ConvertToScribanValue(stats);
            }
            context.PushGlobal(scriptObject);

            return template.Render(context);
        }
        catch (Exception ex)
        {
            LogBodyRenderingFailed(logger, ex);
            return bodyContent; // Return original content on error
        }
    }

    /// <summary>
    /// Converts a value (including JsonElement) to a Scriban-compatible type.
    /// </summary>
    private static object? ConvertToScribanValue(object? value)
    {
        if (value is null)
        {
            return null;
        }

        if (value is System.Text.Json.JsonElement jsonElement)
        {
            return ConvertJsonElement(jsonElement);
        }

        return value;
    }

    /// <summary>
    /// Converts a JsonElement to Scriban-compatible types (ScriptObject, ScriptArray, primitives).
    /// </summary>
    private static object? ConvertJsonElement(System.Text.Json.JsonElement element)
    {
        return element.ValueKind switch
        {
            System.Text.Json.JsonValueKind.Object => ConvertJsonObject(element),
            System.Text.Json.JsonValueKind.Array => ConvertJsonArray(element),
            System.Text.Json.JsonValueKind.String => element.GetString(),
            System.Text.Json.JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
            System.Text.Json.JsonValueKind.True => true,
            System.Text.Json.JsonValueKind.False => false,
            System.Text.Json.JsonValueKind.Null => null,
            System.Text.Json.JsonValueKind.Undefined => null,
            _ => element.ToString()
        };
    }

    /// <summary>
    /// Converts a JSON object to a ScriptObject.
    /// </summary>
    private static ScriptObject ConvertJsonObject(System.Text.Json.JsonElement element)
    {
        var obj = new ScriptObject();
        foreach (var prop in element.EnumerateObject())
        {
            obj[prop.Name] = ConvertJsonElement(prop.Value);
        }
        return obj;
    }

    /// <summary>
    /// Converts a JSON array to a ScriptArray.
    /// </summary>
    private static ScriptArray ConvertJsonArray(System.Text.Json.JsonElement element)
    {
        var arr = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            arr.Add(ConvertJsonElement(item));
        }
        return arr;
    }

    /// <summary>
    /// Converts any JsonElement values in a ScriptObject to Scriban-compatible types.
    /// </summary>
    /// <remarks>
    /// This handles data sources loaded from JSON files via the data: frontmatter field.
    /// Variable names are user-defined (e.g., "statistics", "galleries", "images").
    /// </remarks>
    private static void ConvertJsonElementsInScriptObject(ScriptObject scriptObject)
    {
        // Collect keys to convert (can't modify during enumeration)
        var keysToConvert = new List<string>();
        foreach (var key in scriptObject.Keys)
        {
            if (scriptObject[key] is System.Text.Json.JsonElement)
            {
                keysToConvert.Add(key);
            }
        }

        // Convert each JsonElement
        foreach (var key in keysToConvert)
        {
            if (scriptObject[key] is System.Text.Json.JsonElement jsonElement)
            {
                scriptObject[key] = ConvertJsonElement(jsonElement);
            }
        }
    }

    /// <summary>
    /// Create Scriban context with custom functions and template loader
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method calls <c>ScriptObject.Import</c> overloads that Scriban
    /// annotates with <see cref="RequiresUnreferencedCodeAttribute"/> (10×
    /// IL2026). Both flavors are trim-safe in our usage:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <c>Import(model)</c> — <paramref name="model"/> is always a
    /// <see cref="IDictionary{TKey, TValue}"/> built in
    /// <c>RenderService</c>. Scriban iterates dictionary entries directly
    /// (no property reflection), so trimming cannot remove anything the
    /// runtime needs.
    /// </item>
    /// <item>
    /// <c>Import(name, delegate)</c> — each delegate wraps a static method
    /// of this class with a fully-known signature. The trimmer keeps those
    /// methods because they are referenced via <c>new Func&lt;...&gt;(Method)</c>.
    /// Scriban's <c>DynamicCustomFunction</c> uses
    /// <see cref="System.Reflection.MethodInfo"/> from the delegate at
    /// runtime, which works against the preserved metadata.
    /// </item>
    /// </list>
    /// <para>
    /// Suppressed at method scope rather than per-call: every call site here
    /// shares the same justification, and extracting nine local helpers would
    /// be pure noise.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming",
        Justification = "Scriban.Import on a Dictionary takes the entry-iteration path (no reflection); delegate imports wrap static methods preserved via Func<> references.")]
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
            LoopLimit = 0,

            // Disable output size limit (default 1 MiB / 1048576). Per Scriban docs:
            // "Caps string materialization and rendered output growth. Scriban truncates
            //  output with ... when the limit is reached. Set to 0 to disable the limit."
            // https://scriban.github.io/docs/runtime/safe-runtime/
            // Large galleries (~200 images × multiple formats × srcset sizes) easily
            // exceed 1 MiB of HTML per page, producing silently truncated output.
            LimitToString = 0,

            // Surface runtime exceptions to the logger and re-throw instead of allowing
            // Scriban to silently abort rendering and return truncated output.
            RenderRuntimeException = ex =>
            {
                LogScribanRuntimeException(logger, ex.Message, ex.Span.FileName ?? "(template)", ex.Span.Start.Line, ex.Span.Start.Column);
                throw ex;
            }
        };

        // Set up template loader for partials if theme is set
        if (currentTheme is not null)
        {
            context.TemplateLoader = new TemplateResolverLoader(templateResolver, compiledTemplates, logger);
        }

        var scriptObject = new ScriptObject();

        // Import model as global variables.
        // Trim-safe explicit copy for dictionaries: Scriban's Import(object) uses
        // reflection to detect IDictionary<string, object?> and dispatch to entry
        // iteration. That reflection-based dispatch breaks under PublishTrimmed —
        // the trimmer cannot prove the interface check is reachable. We do the
        // copy ourselves so trimming never sees a reflection path on the model
        // container. Values inside the dictionary are already trim-safe
        // (primitives, ScriptObject from [RevelaTemplateModel] codegen, JsonElement).
        if (model is IDictionary<string, object?> dict)
        {
            foreach (var (key, value) in dict)
            {
                scriptObject[key] = value;
            }
        }
        else
        {
            scriptObject.Import(model);
        }

        // Convert any JsonElement values in the model to Scriban-compatible types
        // This is needed for custom templates that use data: field with JSON files
        ConvertJsonElementsInScriptObject(scriptObject);

        // Convert any JsonElement values in the model to Scriban-compatible types
        // This is needed for custom templates that use data: field with JSON files
        ConvertJsonElementsInScriptObject(scriptObject);

        // Capture the per-render link context so URL helpers own all basepath /
        // baseUrl knowledge (templates never concatenate paths themselves).
        var basePath = GetStringValue(scriptObject, "basepath");
        var assetsBasePath = GetStringValue(scriptObject, "assets_basepath");
        var baseUrl = GetStringValue(scriptObject, "base_url");

        // Register custom functions
        scriptObject.Import("page_url", new Func<object?, string?>(target => PageUrl(target, basePath)));
        scriptObject.Import("absolute_url", new Func<object?, string>(target => AbsoluteUrl(target, baseUrl)));
        scriptObject.Import("variant_url", new Func<object?, int, string, string>((image, size, format) => VariantUrl(image, size, format, assetsBasePath)));
        scriptObject.Import("asset_url", new Func<string, string>(AssetUrl));
        scriptObject.Import("format_date", new Func<DateTime, string, string>(FormatDate));
        scriptObject.Import("format_filesize", new Func<long, string>(FormatFileSize));
        scriptObject.Import("format_exif_exposure", new Func<double?, string>(FormatExifExposure));
        scriptObject.Import("format_exif_aperture", new Func<double?, string>(FormatExifAperture));
        scriptObject.Import("markdown", new Func<string?, string>(Markdown));
        scriptObject.Import("find_image", new Func<string, Image?>(path => ResolveImageForTemplate(path, scriptObject)));

        context.PushGlobal(scriptObject);

        return context;
    }

    // Custom template functions

    /// <summary>
    /// Reads a string-valued global from the render model, or an empty string when absent.
    /// </summary>
    private static string GetStringValue(ScriptObject scriptObject, string key) =>
        scriptObject.TryGetValue(key, out var value) && value is string text ? text : string.Empty;

    /// <summary>
    /// Site-root-relative page URL for a link target, resolved against the current
    /// page's base path. Polymorphic: accepts an <see cref="Image"/>, <see cref="Gallery"/>,
    /// <see cref="NavigationItem"/>, their Scriban projections, or a raw slug string.
    /// </summary>
    /// <remarks>
    /// Returns <c>null</c> for a navigation item that has no page (a section
    /// header), so templates can branch on <c>{{ if page_url(item) }}</c>
    /// (Scriban treats an empty string as truthy, but <c>null</c> as falsy).
    /// </remarks>
    /// <example>{{ page_url(gallery) }} → /events/fireworks/</example>
    private static string? PageUrl(object? target, string basePath)
    {
        var path = ResolveTargetPath(target);
        return path.Length == 0 ? null : basePath + path;
    }

    /// <summary>
    /// Absolute URL (including host from <c>baseUrl</c>) for a link target. Same
    /// polymorphism as <see cref="PageUrl"/>. Intended for Open Graph, RSS, sitemap
    /// and JSON-LD output.
    /// </summary>
    /// <remarks>
    /// When <c>baseUrl</c> is unset the root-relative form is returned instead of
    /// throwing, consistent with the check hint that absolute URLs are skipped
    /// without a configured base URL.
    /// </remarks>
    /// <example>{{ absolute_url(gallery) }} → https://example.com/events/fireworks/</example>
    private static string AbsoluteUrl(object? target, string baseUrl)
    {
        var rootRelative = "/" + ResolveTargetPath(target);
        return baseUrl.Length == 0 ? rootRelative : baseUrl + rootRelative;
    }

    /// <summary>
    /// Asset URL for a specific image variant (size and format), resolved against
    /// the current page's asset base path (relative directory or CDN URL).
    /// </summary>
    /// <example>{{ variant_url(image, 640, 'jpg') }} → ../images/events/fireworks/029081/640.jpg</example>
    private static string VariantUrl(object? image, int size, string format, string assetsBasePath)
    {
        var slug = ResolveImageSlug(image);
        return $"{assetsBasePath}{slug}/{size.ToString(CultureInfo.InvariantCulture)}.{format}";
    }

    /// <summary>
    /// Resolves the site-relative page path (no host, no per-page base path) for a
    /// link target. Empty string when the target has no page.
    /// </summary>
    private static string ResolveTargetPath(object? target) => target switch
    {
        null => string.Empty,
        Image image => ImagePagePath(image.Slug),
        Gallery gallery => NormalizeDirectory(gallery.Slug),
        NavigationItem item => NormalizeDirectory(item.Url),
        string slug => NormalizeDirectory(slug),
        ScriptObject so => ResolveTargetPathFromScriptObject(so),
        _ => string.Empty
    };

    private static string ResolveTargetPathFromScriptObject(ScriptObject so)
    {
        // Image projection carries width/height; distinguish it from a Gallery that
        // also exposes a slug so image links get their dedicated /image/ prefix.
        if (so.ContainsKey("width") && so.ContainsKey("height") && so.TryGetValue("slug", out var imageSlug))
        {
            return ImagePagePath(imageSlug as string ?? string.Empty);
        }

        if (so.TryGetValue("slug", out var slug))
        {
            return NormalizeDirectory(slug as string);
        }

        if (so.TryGetValue("url", out var url))
        {
            return NormalizeDirectory(url as string);
        }

        return string.Empty;
    }

    private static string ResolveImageSlug(object? image) => image switch
    {
        Image typed => typed.Slug.Trim('/').Replace('\\', '/'),
        string slug => slug.Trim('/').Replace('\\', '/'),
        ScriptObject so when so.TryGetValue("slug", out var slug) => (slug as string ?? string.Empty).Trim('/').Replace('\\', '/'),
        _ => string.Empty
    };

    private static string ImagePagePath(string slug)
    {
        var normalized = slug.Trim('/').Replace('\\', '/');
        return normalized.Length == 0 ? string.Empty : $"image/{normalized}/";
    }

    /// <summary>
    /// Normalizes a slug/path segment to a directory-style path: forward slashes,
    /// no leading slash, exactly one trailing slash. Empty input yields empty output.
    /// </summary>
    private static string NormalizeDirectory(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalized = path.Replace('\\', '/').Trim('/');
        return normalized.Length == 0 ? string.Empty : normalized + "/";
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

        return string.Format(CultureInfo.InvariantCulture, "{0:0.##} {1}", len, sizes[order]);
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
    /// Convert Markdown text to HTML.
    /// </summary>
    /// <example>{{ content | markdown }}</example>
    private string Markdown(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        return markdownService.ToHtml(text);
    }

    /// <summary>
    /// Resolve an image path for the <c>image</c> template function.
    /// </summary>
    /// <remarks>
    /// Uses the same 3-step lookup as content images in Markdown:
    /// <list type="number">
    /// <item>Gallery-local: <c>{gallery.path}/{path}</c></item>
    /// <item>Shared images: <c>_images/{path}</c></item>
    /// <item>Exact match: <c>{path}</c> as-is</item>
    /// </list>
    /// </remarks>
    private Image? ResolveImageForTemplate(string imagePath, ScriptObject scriptObject)
    {
        if (imageLookup is null)
        {
            return null;
        }

        var normalizedPath = imagePath.Replace('\\', '/');

        // Get gallery path from the model (may be null for non-gallery contexts)
        var galleryPath = string.Empty;
        if (scriptObject.TryGetValue("gallery", out var galleryObj) && galleryObj is ScriptObject galleryScript)
        {
            if (galleryScript.TryGetValue("path", out var pathObj) && pathObj is string path)
            {
                galleryPath = path;
            }
        }

        // 1. Gallery-local
        if (!string.IsNullOrEmpty(galleryPath))
        {
            var localPath = $"{galleryPath}/{normalizedPath}";
            if (imageLookup.TryGetValue(localPath, out var localImage))
            {
                return localImage;
            }
        }

        // 2. Shared images: _images/{path}
        var sharedPath = $"{ProjectPaths.SharedImages}/{normalizedPath}";
        if (imageLookup.TryGetValue(sharedPath, out var sharedImage))
        {
            return sharedImage;
        }

        // 3. Exact match
        if (imageLookup.TryGetValue(normalizedPath, out var exactImage))
        {
            return exactImage;
        }

        return null;
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Scriban runtime exception: {Message} at {File}:{Line}:{Column}")]
    private static partial void LogScribanRuntimeException(ILogger logger, string message, string file, int line, int column);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Rendering template: {Path}")]
    private static partial void LogRenderingTemplate(ILogger logger, string path);

    [LoggerMessage(Level = LogLevel.Information, Message = "Template cache cleared")]
    private static partial void LogCacheCleared(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme set for template engine: {ThemeName}")]
    private static partial void LogThemeSet(ILogger logger, string themeName);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Theme extension set: {ExtensionName} (prefix: {Prefix})")]
    private static partial void LogExtensionSet(ILogger logger, string extensionName, string prefix);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Body template parsing failed: {Errors}")]
    private static partial void LogBodyTemplateParsingFailed(ILogger logger, string errors);

    [LoggerMessage(Level = LogLevel.Error, Message = "Body template rendering failed")]
    private static partial void LogBodyRenderingFailed(ILogger logger, Exception exception);
}

/// <summary>
/// Template loader using ITemplateResolver for scan-based template loading.
/// </summary>
/// <remarks>
/// <para>
/// Supports the Scriban include directive:
/// <code>
/// {{ include 'gallery' }}              {{~ // → body/gallery ~}}
/// {{ include 'statistics/overview' }}  {{~ // → partials/statistics/overview ~}}
/// </code>
/// </para>
/// <para>
/// Resolution is handled by ITemplateResolver which scans:
/// 1. Local project overrides (highest priority)
/// 2. Theme extensions (by prefix)
/// 3. Base theme (lowest priority)
/// </para>
/// </remarks>
internal sealed partial class TemplateResolverLoader(
    ITemplateResolver resolver,
    ConcurrentDictionary<string, Template> templateCache,
    ILogger logger) : ITemplateLoader
{
    /// <summary>
    /// Get the path for a template include (used as cache key)
    /// </summary>
    /// <remarks>
    /// <para>
    /// Template key conventions:
    /// - Include without prefix → implicitly partials/ (e.g., "navigation" → "partials/navigation")
    /// - Include with body/ prefix → explicit body template (e.g., "body/gallery")
    /// - Include with partials/ prefix → kept as-is for backward compatibility
    /// </para>
    /// <para>
    /// This allows users to write cleaner includes:
    /// {{ include 'navigation' }} instead of {{ include 'partials/navigation' }}
    /// </para>
    /// </remarks>
    [SuppressMessage(
        "Globalization",
        "CA1308:Normalize strings to uppercase",
        Justification = "Template keys use lowercase by convention - this is format conversion, not normalization")]
    public string GetPath(TemplateContext context, SourceSpan callerSpan, string templateName)
    {
        // Normalize: remove .revela extension if present, use lowercase
        var key = templateName.EndsWith(".revela", StringComparison.OrdinalIgnoreCase)
            ? templateName[..^7]
            : templateName;

        key = key.ToLowerInvariant();

        // Add implicit partials/ prefix if no prefix specified
        // body/ and partials/ prefixes are kept as-is
        if (!key.StartsWith("body/", StringComparison.Ordinal) &&
            !key.StartsWith("partials/", StringComparison.Ordinal))
        {
            key = "partials/" + key;
        }

        return key;
    }

    /// <summary>
    /// Load template content via ITemplateResolver
    /// </summary>
    public string Load(TemplateContext context, SourceSpan callerSpan, string templatePath)
    {
        // Check our compiled template cache first
        if (templateCache.TryGetValue(templatePath, out var cachedTemplate))
        {
            // Template already compiled, but Scriban needs the source for include
            // We need to return the source content for Scriban's internal handling
            // Get it from the resolver again (streams are cheap)
            using var cachedStream = resolver.GetTemplate(templatePath);
            if (cachedStream is not null)
            {
                using var cachedReader = new StreamReader(cachedStream);
                return cachedReader.ReadToEnd();
            }
        }

        // Get template from resolver
        using var stream = resolver.GetTemplate(templatePath)
            ?? throw new FileNotFoundException(
                $"Template '{templatePath}' not found. " +
                $"If this is a plugin template, ensure the theme extension is installed.",
                templatePath);

        LogLoadingTemplate(logger, templatePath);
        return LoadFromStream(stream, templatePath);
    }

    private string LoadFromStream(Stream stream, string templatePath)
    {
        using var reader = new StreamReader(stream);
        var content = reader.ReadToEnd();

        // Parse and cache the template
        var template = Template.Parse(content);
        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template parsing failed ({templatePath}): {errors}");
        }

        templateCache[templatePath] = template;

        return content;
    }

    /// <summary>
    /// Async version of Load (required by interface)
    /// </summary>
    public ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath) =>
        new(Load(context, callerSpan, templatePath));

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading template: {TemplatePath}")]
    private static partial void LogLoadingTemplate(ILogger logger, string templatePath);
}




