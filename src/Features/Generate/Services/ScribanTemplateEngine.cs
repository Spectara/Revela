using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Net;
using System.Text;
using Scriban;
using Scriban.Parsing;
using Scriban.Runtime;
using Scriban.Syntax;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Core.Themes;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
#pragma warning disable IDE0005 // Using directive is unnecessary — namespace holds source-generated extension methods the analyzer cannot see.
using Spectara.Revela.Sdk.TemplateModels;
#pragma warning restore IDE0005

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
/// <para>
/// One engine serves one render run and is shared by all pages, also when they render in
/// parallel. Every template and include is parsed once per engine; each render gets its own
/// <see cref="TemplateContext"/>, as Scriban requires for concurrent use.
/// </para>
/// </remarks>
internal sealed partial class ScribanTemplateEngine(
    ILogger<ScribanTemplateEngine> logger,
    IMarkdownService markdownService,
    ITemplateResolver templateResolver) : ITemplateEngine
{
    private readonly ConcurrentDictionary<string, Template> parsedTemplates = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, Template> parsedIncludes = new(StringComparer.Ordinal);
    private IReadOnlyDictionary<string, Image> imageLookup = new Dictionary<string, Image>();
    private ThemeStrings strings = ThemeStrings.Empty;
    private TranslateFunction translateFunction = new(ThemeStrings.Empty);
    private string ogLocale = ToOpenGraphLocale(ThemeStrings.Empty.Language);

    private static readonly SearchValues<char> HtmlSpecialCharacters = SearchValues.Create("&<>\"'");

    /// <inheritdoc />
    public void SetImageLookup(IReadOnlyDictionary<string, Image> imagesBySourcePath) =>
        imageLookup = imagesBySourcePath;

    /// <inheritdoc />
    public void SetStrings(ThemeStrings themeStrings)
    {
        strings = themeStrings;
        translateFunction = new TranslateFunction(themeStrings);
        ogLocale = ToOpenGraphLocale(themeStrings.Language);
    }

    /// <inheritdoc />
    public string Render(string templateContent, IReadOnlyDictionary<string, object?> model)
    {
        try
        {
            var template = parsedTemplates.GetOrAdd(templateContent, Parse);
            return template.Render(CreateScriptContext(model));
        }
        catch (Exception ex)
        {
            LogRenderingFailed(logger, ex);
            throw;
        }
    }

    private static Template Parse(string templateContent)
    {
        var template = Template.Parse(templateContent);
        if (template.HasErrors)
        {
            var errors = string.Join(", ", template.Messages.Select(m => m.Message));
            throw new InvalidOperationException($"Template parsing failed: {errors}");
        }

        return template;
    }

    /// <summary>
    /// Create Scriban context with custom functions and template loader
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every model value is already Scriban-native (primitives, strings, string lists,
    /// <see cref="ScriptObject"/>/<see cref="ScriptArray"/> from <c>[RevelaTemplateModel]</c>
    /// codegen or converted JSON), so Scriban never reflects over .NET objects — a requirement
    /// for the trimmed Native AOT build.
    /// </para>
    /// <para>
    /// <c>Import(name, delegate)</c> is annotated with <see cref="RequiresUnreferencedCodeAttribute"/>.
    /// Each delegate here wraps a method of this class with a fully-known signature, which the
    /// trimmer keeps because it is referenced via <c>new Func&lt;...&gt;(...)</c>. Scriban's
    /// <c>DynamicCustomFunction</c> uses the delegate's <see cref="System.Reflection.MethodInfo"/>,
    /// which works against the preserved metadata.
    /// </para>
    /// </remarks>
    [UnconditionalSuppressMessage(
        "Trimming",
        "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming",
        Justification = "Delegate imports wrap methods preserved via Func<> references.")]
    private CachingTemplateContext CreateScriptContext(IReadOnlyDictionary<string, object?> model)
    {
        var context = new CachingTemplateContext(parsedIncludes)
        {
            TemplateLoader = new TemplateResolverLoader(templateResolver, logger),

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

        var scriptObject = new ScriptObject();
        foreach (var (key, value) in model)
        {
            scriptObject[key] = value;
        }

        // Capture the per-render link context so URL helpers own all basepath /
        // baseUrl knowledge (templates never concatenate paths themselves).
        var basePath = GetStringValue(scriptObject, "basepath");
        var assetsBasePath = GetStringValue(scriptObject, "assets_basepath");
        var baseUrl = GetStringValue(scriptObject, "base_url");
        scriptObject.TryGetValue("gallery", out var currentGallery);
        scriptObject.TryGetValue("photo", out var currentPhoto);
        scriptObject.TryGetValue("image", out var currentImage);
        var currentPagePath = currentPhoto is null ? ResolveTargetPath(currentGallery) : ImagePagePath(ResolveImageSlug(currentImage));
        var galleryPath = currentGallery is ScriptObject gallery && gallery.TryGetValue("path", out var path) && path is string text
            ? text
            : string.Empty;

        // Register custom functions
        scriptObject.Import("page_url", new Func<object?, string?>(target => PageUrl(target, basePath)));
        scriptObject.Import("absolute_url", new Func<object?, string>(target => AbsoluteUrl(target, baseUrl, basePath)));
        scriptObject.Import("variant_url", new Func<object?, int, string, string>((image, size, format) => VariantUrl(image, size, format, assetsBasePath)));
        scriptObject.Import("absolute_variant_url", new Func<object?, int, string, string>((image, size, format) =>
            AbsoluteVariantUrl(image, size, format, assetsBasePath, baseUrl, basePath, currentPagePath)));
        scriptObject.Import("asset_url", new Func<string?, string>(path => AssetUrl(path, basePath)));
        var culture = strings.Culture;
        scriptObject.Import("format_date", new Func<DateTime, string, string>((date, format) => FormatDate(date, format, culture)));
        scriptObject.Import("format_filesize", new Func<long, string>(bytes => FormatFileSize(bytes, culture)));
        scriptObject.Import("format_exif_exposure", new Func<double?, string>(FormatExifExposure));
        scriptObject.Import("format_exif_aperture", new Func<double?, string>(FormatExifAperture));
        scriptObject.Import("html_escape", new Func<object?, string>(HtmlEscape));
        scriptObject.Import("markdown", new Func<string?, string>(Markdown));
        scriptObject.Import("find_image", new Func<string, ScriptObject?>(imagePath => FindImage(imagePath, galleryPath)));
        scriptObject["t"] = translateFunction;
        scriptObject["og_locale"] = ogLocale;

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
    /// Escapes only the HTML-significant characters (<c>&amp; &lt; &gt; " '</c>), safe for text and
    /// quoted attribute values. Unlike <see cref="WebUtility.HtmlEncode(string)"/>, non-ASCII text such
    /// as umlauts stays literal so the UTF-8 source remains readable.
    /// </summary>
    internal static string HtmlEscape(object? value)
    {
        var text = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
        if (text.AsSpan().IndexOfAny(HtmlSpecialCharacters) < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length + 16);
        foreach (var c in text)
        {
            _ = c switch
            {
                '&' => builder.Append("&amp;"),
                '<' => builder.Append("&lt;"),
                '>' => builder.Append("&gt;"),
                '"' => builder.Append("&quot;"),
                '\'' => builder.Append("&#39;"),
                _ => builder.Append(c)
            };
        }

        return builder.ToString();
    }

    /// <summary>
    /// Open Graph locale (<c>language_TERRITORY</c>, e.g. <c>de_DE</c>) for a site language, using the
    /// language's default territory when none is given. Empty when the language is unknown.
    /// </summary>
    private static string ToOpenGraphLocale(string language)
    {
        try
        {
            var culture = CultureInfo.CreateSpecificCulture(language.Trim().Replace('_', '-'));
            return culture.Name.Replace('-', '_');
        }
        catch (CultureNotFoundException)
        {
            return string.Empty;
        }
    }

    /// <summary>
    /// Site-root-relative page URL for a link target, resolved against the current
    /// page's base path. Polymorphic: accepts the Scriban projection of an image, gallery or
    /// navigation item, or a raw slug string.
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
    /// Page URL of an image's photo page, the same URL <c>page_url(image)</c> returns in templates.
    /// </summary>
    internal static string PhotoPageUrl(string imageSlug, string basePath) => basePath + ImagePagePath(imageSlug);

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
    private static string AbsoluteUrl(object? target, string baseUrl, string basePath)
    {
        var resolved = new Uri(CreateSiteRoot(baseUrl, basePath), ResolveTargetPath(target));
        return baseUrl.Length == 0 ? resolved.PathAndQuery : resolved.AbsoluteUri;
    }

    private static Uri CreateSiteRoot(string baseUrl, string basePath)
    {
        var origin = new Uri(baseUrl.Length == 0 ? "https://revela.invalid/" : baseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        return basePath.StartsWith('/') ? new Uri(origin, basePath.TrimEnd('/') + "/") : origin;
    }

    private static string AbsoluteVariantUrl(object? image, int size, string format, string assetsBasePath, string baseUrl, string basePath, string currentPagePath)
    {
        var variant = VariantUrl(image, size, format, assetsBasePath);
        if (Uri.TryCreate(variant, UriKind.Absolute, out var absolute) &&
            (absolute.Scheme == Uri.UriSchemeHttps || absolute.Scheme == Uri.UriSchemeHttp))
        {
            return absolute.AbsoluteUri;
        }

        var currentPage = new Uri(CreateSiteRoot(baseUrl, basePath), currentPagePath);
        var resolved = new Uri(currentPage, variant);
        return baseUrl.Length == 0 && !variant.StartsWith("//", StringComparison.Ordinal)
            ? resolved.PathAndQuery
            : resolved.AbsoluteUri;
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
        string slug => NormalizeDirectory(slug),
        ScriptObject so => ResolveTargetPathFromScriptObject(so),
        _ => string.Empty
    };

    private static string ResolveTargetPathFromScriptObject(ScriptObject so)
    {
        // Image projection carries width/height; distinguish it from a Gallery that
        // also exposes a slug so image links get their dedicated /photo/ prefix.
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
        string slug => slug.Trim('/').Replace('\\', '/'),
        ScriptObject so when so.TryGetValue("slug", out var slug) => (slug as string ?? string.Empty).Trim('/').Replace('\\', '/'),
        _ => string.Empty
    };

    private static string ImagePagePath(string slug)
    {
        var normalized = slug.Trim('/').Replace('\\', '/');
        return normalized.Length == 0 ? string.Empty : $"photo/{normalized}/";
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
    /// URL for a theme asset (CSS, JS, fonts) written to the output's <c>_assets/</c>
    /// folder, resolved against the current page's base path (relative directory
    /// or configured subdirectory like <c>/photos/</c>).
    /// </summary>
    /// <example>{{ asset_url "main.css" }} → ../_assets/main.css</example>
    private static string AssetUrl(string? path, string basePath)
    {
        var normalized = (path ?? string.Empty).Replace('\\', '/').Trim('/');
        return $"{basePath}_assets/{normalized}";
    }

    /// <summary>
    /// Format date with custom format string in the site language's culture
    /// </summary>
    /// <example>{{ format_date image.date_taken "d. MMMM yyyy" }} → 20. Januar 2024 (site.language "de")</example>
    private static string FormatDate(DateTime date, string format, CultureInfo culture)
    {
        try
        {
            return date.ToString(format, culture);
        }
        catch (FormatException)
        {
            return date.ToString("d", culture);
        }
    }

    /// <summary>
    /// Format file size in human-readable format using the site language's number format
    /// </summary>
    /// <example>{{ format_filesize 1048576 }} → 1 MB</example>
    private static string FormatFileSize(long bytes, CultureInfo culture)
    {
        string[] sizes = ["B", "KB", "MB", "GB"];
        double len = bytes;
        var order = 0;

        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }

        return string.Format(culture, "{0:0.##} {1}", len, sizes[order]);
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
    /// The <c>find_image</c> template function: resolves an image path like a Markdown image
    /// (see <see cref="ImagePathResolver"/>) relative to the current page.
    /// </summary>
    /// <example>{{ hero = find_image 'hero.jpg' }}</example>
    private ScriptObject? FindImage(string imagePath, string galleryPath) =>
        ImagePathResolver.Resolve(imagePath, galleryPath, imageLookup)?.ToScriptObject();

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Error, Message = "Template rendering failed")]
    private static partial void LogRenderingFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scriban runtime exception: {Message} at {File}:{Line}:{Column}")]
    private static partial void LogScribanRuntimeException(ILogger logger, string message, string file, int line, int column);

    /// <summary>
    /// Template context that shares parsed includes across all renders of one engine.
    /// </summary>
    /// <remarks>
    /// Scriban caches includes only per context (<see cref="TemplateContext.CachedTemplates"/>),
    /// so without this every page would load and parse its partials again.
    /// </remarks>
    private sealed class CachingTemplateContext(ConcurrentDictionary<string, Template> parsedIncludes) : TemplateContext
    {
        protected override Template CreateTemplate(string templatePath, ScriptNode? callerContext) =>
            parsedIncludes.GetOrAdd(templatePath, path => base.CreateTemplate(path, callerContext));
    }
}

/// <summary>
/// Template loader using ITemplateResolver for scan-based template loading.
/// </summary>
/// <remarks>
/// <para>
/// Supports the Scriban include directive:
/// <code>
/// {{ include 'navigation' }}           {{~ // → partials/navigation ~}}
/// {{ include 'body/gallery' }}         {{~ // → body/gallery ~}}
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
    ILogger logger) : ITemplateLoader
{
    /// <summary>
    /// Get the path for a template include (used as cache key)
    /// </summary>
    /// <remarks>
    /// Includes without a <c>body/</c> or <c>partials/</c> prefix are partials, so templates
    /// write <c>{{ include 'navigation' }}</c> instead of <c>{{ include 'partials/navigation' }}</c>.
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
        using var stream = resolver.GetTemplate(templatePath)
            ?? throw new FileNotFoundException(
                $"Template '{templatePath}' not found. " +
                $"If this is a plugin template, ensure the theme extension is installed.",
                templatePath);

        LogLoadingTemplate(logger, templatePath);
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Async version of Load (required by interface)
    /// </summary>
    public ValueTask<string?> LoadAsync(TemplateContext context, SourceSpan callerSpan, string templatePath) =>
        new(Load(context, callerSpan, templatePath));

    [LoggerMessage(Level = LogLevel.Debug, Message = "Loading template: {TemplatePath}")]
    private static partial void LogLoadingTemplate(ILogger logger, string templatePath);
}
