using System.Text.Json;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Commands.Generate.Parsing;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Models.Manifest;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for template rendering and HTML page generation.
/// </summary>
/// <remarks>
/// <para>
/// Renders HTML pages from manifest data using Scriban templates.
/// Copies theme assets to output directory.
/// </para>
/// </remarks>
public sealed partial class RenderService(
    ITemplateEngine templateEngine,
    IThemeResolver themeResolver,
    ITemplateResolver templateResolver,
    IAssetResolver assetResolver,
    IStaticFileService staticFileService,
    IManifestRepository manifestRepository,
    RevelaParser revelaParser,
    IMarkdownService markdownService,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<ProjectConfig> projectConfig,
    IOptionsMonitor<GenerateConfig> options,
    IOptionsMonitor<ThemeConfig> themeConfig,
    ILogger<RenderService> logger) : IRenderService
{
    /// <summary>Current theme extensions (set during rendering)</summary>
    private IReadOnlyList<IThemeExtension> currentExtensions = [];

    /// <summary>Gets full path to source directory</summary>
    private string SourcePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Source);

    /// <summary>Gets full path to output directory</summary>
    private string OutputPath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Output);

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => options.CurrentValue.Images;

    /// <inheritdoc />
    public void SetTheme(IThemePlugin? theme) => templateEngine.SetTheme(theme);

    /// <inheritdoc />
    public void SetExtensions(IReadOnlyList<IThemeExtension> extensions)
    {
        currentExtensions = extensions;
        templateEngine.SetExtensions(extensions);
    }

    /// <inheritdoc />
    public string Render(string templateContent, object model) => templateEngine.Render(templateContent, model);

    /// <inheritdoc />
    public async Task<string> RenderFileAsync(
        string templatePath,
        object model,
        CancellationToken cancellationToken = default) => await templateEngine.RenderFileAsync(templatePath, model, cancellationToken);

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Load manifest
            await manifestRepository.LoadAsync(cancellationToken);

            if (manifestRepository.Root is null)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage = "No content in manifest. Run scan first."
                };
            }

            // Load configuration
            var config = LoadConfiguration();

            // Resolve theme and extensions
            var theme = themeResolver.Resolve(config.ThemeName, projectEnvironment.Value.Path);
            SetTheme(theme);

            // Get theme extensions matching this theme
            var extensions = themeResolver.GetExtensions(config.ThemeName);
            SetExtensions(extensions);

            // Initialize template resolver (scans theme, extensions, local overrides)
            if (theme is not null)
            {
                templateResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);
                assetResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);
            }

            // Reconstruct galleries and navigation from unified root
            var galleries = ReconstructGalleries(manifestRepository.Root);
            var navigation = ReconstructNavigation(manifestRepository.Root);

            progress?.Report(new RenderProgress
            {
                CurrentPage = "Preparing...",
                Rendered = 0,
                Total = galleries.Count // Galleries already includes root as home
            });

            // Build site model
            var allImages = new List<Image>();
            FlattenImages(galleries, allImages);

            var siteModel = new SiteModel
            {
                Site = config.Site,
                Project = config.Project,
                Galleries = galleries,
                Navigation = navigation,
                Images = allImages,
                BuildDate = DateTime.UtcNow
            };

            // Render templates
            var pageCount = await RenderSiteAsync(siteModel, config, theme, progress, cancellationToken);

            // Copy assets (theme, extensions, local overrides)
            if (theme is not null)
            {
                await assetResolver.CopyToOutputAsync(OutputPath, cancellationToken);
            }

            // Copy static files (source/_static/ → output/)
            await staticFileService.CopyStaticFilesAsync(SourcePath, OutputPath, cancellationToken);

            progress?.Report(new RenderProgress
            {
                CurrentPage = "Complete",
                Rendered = pageCount,
                Total = pageCount
            });

            LogPagesGenerated(logger, pageCount);
            stopwatch.Stop();

            return new RenderResult
            {
                Success = true,
                PageCount = pageCount,
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogPagesGenerationFailed(logger, ex);
            return new RenderResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    #region Private Methods - Configuration

    private RenderContext LoadConfiguration()
    {
        var project = projectConfig.CurrentValue;

        // Get theme name from ThemeConfig (IOptions pattern)
        // Fallback to "Lumina" if not configured
        var themeName = themeConfig.CurrentValue.Name;
        if (string.IsNullOrEmpty(themeName))
        {
            themeName = "Lumina";
        }

        return new RenderContext
        {
            Project = new RenderProjectSettings
            {
                Name = !string.IsNullOrEmpty(project.Name) ? project.Name : "Revela Site",
                BaseUrl = !string.IsNullOrEmpty(project.BaseUrl) ? project.BaseUrl : "https://example.com",
                Language = !string.IsNullOrEmpty(project.Language) ? project.Language : "en",
                ImageBasePath = project.ImageBasePath,
                BasePath = NormalizeBasePath(project.BasePath)
            },
            Site = LoadSiteJson(),
            ThemeName = themeName
        };
    }

    /// <summary>
    /// Load site.json directly as JsonElement for dynamic template access.
    /// </summary>
    /// <returns>JsonElement with site properties, or null if site.json doesn't exist.</returns>
    private JsonElement? LoadSiteJson()
    {
        var siteJsonPath = Path.Combine(projectEnvironment.Value.Path, "site.json");
        if (!File.Exists(siteJsonPath))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(siteJsonPath);
            return JsonDocument.Parse(json).RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string NormalizeBasePath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return "/";
        }

        var normalized = basePath.Trim();
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }
        return normalized;
    }

    #endregion

    #region Private Methods - Reconstruction

    /// <summary>
    /// Reconstruct galleries from the unified root tree.
    /// </summary>
    /// <remarks>
    /// Galleries are nodes with a non-null slug (meaning they have a page).
    /// </remarks>
    private static List<Gallery> ReconstructGalleries(ManifestEntry root)
    {
        var galleries = new List<Gallery>();

        // Add root as home gallery if it has a slug
        if (!string.IsNullOrEmpty(root.Slug) || string.IsNullOrEmpty(root.Path))
        {
            galleries.Add(ReconstructGalleryFromEntry(root));
        }

        // Recursively find all gallery nodes
        CollectGalleries(root.Children, galleries);

        return galleries;
    }

    private static void CollectGalleries(IReadOnlyList<ManifestEntry> entries, List<Gallery> galleries)
    {
        foreach (var entry in entries)
        {
            // Only nodes with a slug are galleries (leaf nodes with pages)
            if (!string.IsNullOrEmpty(entry.Slug))
            {
                galleries.Add(ReconstructGalleryFromEntry(entry));
            }

            // Recurse into children
            CollectGalleries(entry.Children, galleries);
        }
    }

    private static Gallery ReconstructGalleryFromEntry(ManifestEntry entry)
    {
        var images = new List<Image>();
        foreach (var imageEntry in entry.Content.OfType<ImageContent>())
        {
            // Use SourcePath if available (for filtered images from _images),
            // otherwise construct from entry.Path + filename (for regular gallery images)
            var sourcePath = !string.IsNullOrEmpty(imageEntry.SourcePath)
                ? imageEntry.SourcePath
                : string.IsNullOrEmpty(entry.Path)
                    ? imageEntry.Filename
                    : $"{entry.Path}/{imageEntry.Filename}";
            // Normalize any remaining backslashes for cross-platform consistency
            images.Add(Image.FromManifestEntry(sourcePath.Replace('\\', '/'), imageEntry));
        }

        return new Gallery
        {
            Path = entry.Path,
            Slug = entry.Slug ?? string.Empty,
            Name = entry.Text,
            Title = entry.Text,
            Description = entry.Description,
            Template = entry.Template,
            DataSources = entry.DataSources,
            Cover = entry.Cover,
            Date = entry.Date,
            Featured = entry.Featured,
            Weight = 0, // Weight removed from new structure
            Images = images,
            SubGalleries = [] // Sub-galleries are flattened in the tree
        };
    }

    /// <summary>
    /// Reconstruct navigation from the unified root tree.
    /// </summary>
    private static List<NavigationItem> ReconstructNavigation(ManifestEntry root)
    {
        // Navigation is the children of root (root itself is Home, not in nav)
        return [.. root.Children
            .Where(e => !e.Hidden)
            .Select(ReconstructNavigationItem)];
    }

    private static NavigationItem ReconstructNavigationItem(ManifestEntry entry)
    {
        return new NavigationItem
        {
            Text = entry.Text,
            Url = entry.Slug,
            Description = entry.Description,
            Hidden = entry.Hidden,
            Pinned = entry.Pinned,
            Children = [.. entry.Children
                .Where(e => !e.Hidden)
                .Select(ReconstructNavigationItem)]
        };
    }

    private static void FlattenImages(IEnumerable<Gallery> galleries, List<Image> images)
    {
        foreach (var gallery in galleries)
        {
            images.AddRange(gallery.Images);
            FlattenImages(gallery.SubGalleries, images);
        }
    }

    #endregion

    #region Private Methods - Rendering

    private async Task<int> RenderSiteAsync(
        SiteModel model,
        RenderContext config,
        IThemePlugin? theme,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(OutputPath);

        var manifest = theme?.GetManifest();
        var layoutTemplate = manifest is not null
            ? LoadTemplate(manifest.LayoutTemplate)
            : null;

        var indexTemplate = layoutTemplate
            ?? LoadTemplate("index.revela")
            ?? GetDefaultIndexTemplate();
        var galleryTemplate = layoutTemplate
            ?? LoadTemplate("gallery.revela")
            ?? GetDefaultGalleryTemplate();

        var pageCount = 0;
        var totalPages = model.Galleries.Count; // Galleries already includes root as home

        // Render index page
        progress?.Report(new RenderProgress
        {
            CurrentPage = "index.html",
            Rendered = pageCount,
            Total = totalPages
        });

        var indexNavigation = SetActiveState(model.Navigation, string.Empty);
        var indexBasePath = CalculateSiteBasePath(config, "");
        var indexImageBasePath = CalculateImageBasePath(config, "");
        var themeVariables = theme?.GetManifest().Variables ?? new Dictionary<string, string>();
        var formats = ImageSettings.GetActiveFormats();

        // Get assets from resolver
        var stylesheets = assetResolver.GetStyleSheets();
        var scripts = assetResolver.GetScripts();

        // Find root gallery (home page) - has empty Path
        var rootGallery = model.Galleries.FirstOrDefault(g => string.IsNullOrEmpty(g.Path));

        // Load root gallery metadata (body content, etc.)
        if (rootGallery is not null)
        {
            var (_, _, _) = await LoadGalleryMetadataAsync(rootGallery, cancellationToken);
        }

        // Use root gallery images if available (may be filtered), otherwise all images
        var indexImages = rootGallery?.Images.Count > 0
            ? rootGallery.Images
            : model.Images;

        var indexHtml = templateEngine.Render(
            indexTemplate,
            new
            {
                site = model.Site,
                gallery = rootGallery,
                galleries = model.Galleries,
                images = indexImages,
                nav_items = indexNavigation,
                basepath = indexBasePath,
                image_basepath = indexImageBasePath,
                image_formats = formats.Keys,
                theme = themeVariables,
                stylesheets,
                scripts
            });

        await File.WriteAllTextAsync(
            Path.Combine(OutputPath, "index.html"),
            indexHtml,
            cancellationToken);
        pageCount++;

        // Render gallery pages (skip root gallery, already rendered as index)
        foreach (var gallery in model.Galleries.Where(g => !string.IsNullOrEmpty(g.Path)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new RenderProgress
            {
                CurrentPage = $"{gallery.Slug.TrimEnd('/')}​/index.html",
                Rendered = pageCount,
                Total = totalPages
            });

            // Load metadata from _index.md at render time (not stored in manifest)
            var (customTemplate, dataSources, metadataBasePath) = await LoadGalleryMetadataAsync(gallery, cancellationToken);

            // Use gallery's own images (from manifest Content, populated during scan)
            // Works for both normal galleries and filter galleries
            var galleryImages = gallery.Images.ToList();

            var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
            var basepath = CalculateSiteBasePath(config, relativeBasePath);
            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);
            var galleryImageBasePath = CalculateImageBasePath(config, relativeBasePath);

            // Resolve data sources from data: field (JSON files, $galleries, $images)
            // If no explicit data sources, use extension defaults for the template
            var effectiveDataSources = dataSources;
            if (dataSources.Count == 0 && customTemplate is not null)
            {
                effectiveDataSources = GetExtensionDataDefaults(customTemplate);
            }

            var resolvedData = await ResolveDataSourcesAsync(
                effectiveDataSources,
                metadataBasePath,
                projectEnvironment.Value.Path,
                model.Galleries,
                galleryImages,
                cancellationToken);

            // Custom templates with data sources need pre-rendering to make resolved data available.
            // However, standard body templates (page, gallery) that don't use data sources
            // should NOT be pre-rendered, as they will be included by the layout template.
            // Pre-rendering would cause double-wrapping since the template renders gallery.body
            // which already contains the HTML, and then the layout includes the same template again.
            //
            // Pre-render only when:
            // 1. A custom template is specified AND
            // 2. There are resolved data sources (e.g., statistics data)
            if (customTemplate is not null && resolvedData.Count > 0)
            {
                var contentTemplate = LoadTemplate($"{customTemplate}.revela");
                if (contentTemplate is not null)
                {
                    // Build base model for custom template
                    var baseModel = new Dictionary<string, object?>
                    {
                        ["site"] = model.Site,
                        ["gallery"] = gallery,
                        ["nav_items"] = galleryNavigation,
                        ["basepath"] = basepath,
                        ["image_basepath"] = galleryImageBasePath,
                        ["image_formats"] = formats.Keys,
                        ["theme"] = themeVariables,
                        ["images"] = galleryImages
                    };

                    // Merge resolved data sources (user-defined variable names)
                    foreach (var (key, value) in resolvedData)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        baseModel[key] = value;
                    }

                    // Render the custom template as content
                    var renderedContent = templateEngine.Render(contentTemplate, baseModel);

                    // Set the rendered content as gallery.body
                    gallery.Body = renderedContent;
                }
            }
            // Build layout model - use Dictionary to support dynamic data
            var layoutModel = new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["gallery"] = gallery,
                ["images"] = customTemplate is not null ? [] : galleryImages,
                ["nav_items"] = galleryNavigation,
                ["basepath"] = basepath,
                ["image_basepath"] = galleryImageBasePath,
                ["image_formats"] = formats.Keys,
                ["theme"] = themeVariables,
                ["stylesheets"] = stylesheets,
                ["scripts"] = scripts
            };

            // Add resolved data (e.g., statistics) to layout context
            // This makes data available for body templates included via {{ include body_template }}
            foreach (var (key, value) in resolvedData)
            {
                layoutModel[key] = value;
            }

            // Always use layout template
            var galleryHtml = templateEngine.Render(galleryTemplate, layoutModel);

            var galleryOutputPath = Path.Combine(OutputPath, gallery.Slug);
            Directory.CreateDirectory(galleryOutputPath);

            await File.WriteAllTextAsync(
                Path.Combine(galleryOutputPath, "index.html"),
                galleryHtml,
                cancellationToken);
            pageCount++;
        }

        return pageCount;
    }

    private static List<NavigationItem> SetActiveState(
        IReadOnlyList<NavigationItem> items,
        string currentPath) => [.. items.Select(item => SetActiveStateRecursive(item, currentPath))];

    private static NavigationItem SetActiveStateRecursive(NavigationItem item, string currentPath)
    {
        var isActive = !string.IsNullOrEmpty(item.Url) &&
                      !string.IsNullOrEmpty(currentPath) &&
                      (currentPath.Equals(item.Url, StringComparison.OrdinalIgnoreCase) ||
                       currentPath.StartsWith(item.Url, StringComparison.OrdinalIgnoreCase));

        List<NavigationItem> activeChildren = [.. item.Children.Select(c => SetActiveStateRecursive(c, currentPath))];

        return new NavigationItem
        {
            Text = item.Text,
            Url = item.Url,
            Description = item.Description,
            Active = isActive,
            Hidden = item.Hidden,
            Pinned = item.Pinned,
            Children = activeChildren
        };
    }

    private static string CalculateSiteBasePath(RenderContext config, string relativeBasePath)
    {
        if (config.Project.BasePath == "/")
        {
            return relativeBasePath;
        }
        return config.Project.BasePath;
    }

    private static string CalculateImageBasePath(RenderContext config, string basepath)
    {
        if (!string.IsNullOrEmpty(config.Project.ImageBasePath))
        {
            return config.Project.ImageBasePath;
        }
        return $"{basepath}images/";
    }

    /// <summary>
    /// Loads a template from the theme, extensions, or local overrides via ITemplateResolver.
    /// </summary>
    /// <param name="templateName">Template file name (e.g., "gallery.revela" or "statistics/overview.revela")</param>
    /// <returns>Template content or null if not found</returns>
    private string? LoadTemplate(string templateName)
    {
        // Derive key from template name
        var key = templateName;
        if (key.EndsWith(".revela", StringComparison.OrdinalIgnoreCase))
        {
            key = key[..^7];
        }

        // Add body/ prefix for custom page templates
        // This matches Layout.revela behavior: body_template = 'body/' + (gallery.template ?? 'gallery')
        // Root templates (layout, index, gallery) don't need prefix
        if (!key.StartsWith("body/", StringComparison.OrdinalIgnoreCase) &&
            !key.StartsWith("partials/", StringComparison.OrdinalIgnoreCase) &&
            !IsRootTemplate(key))
        {
            key = "body/" + key;
        }

        // Use template resolver for unified lookup (theme → extensions → local)
        using var stream = templateResolver.GetTemplate(key);
        if (stream is not null)
        {
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        return null;
    }

    /// <summary>
    /// Determines if a template name is a root template (layout, index, gallery).
    /// </summary>
    /// <remarks>
    /// Root templates are NOT prefixed with body/ since they exist at the theme root level.
    /// </remarks>
    private static bool IsRootTemplate(string key) =>
        key.Equals("layout", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("index", StringComparison.OrdinalIgnoreCase) ||
        key.Equals("gallery", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Loads metadata from _index.revela for a gallery at render time.
    /// </summary>
    /// <remarks>
    /// Returns template name, data sources dictionary, and base path.
    /// When template is set (e.g., "statistics/overview"), the page uses a custom template.
    /// </remarks>
    private async Task<(string? Template, IReadOnlyDictionary<string, string> DataSources, string BasePath)> LoadGalleryMetadataAsync(
        Gallery gallery,
        CancellationToken cancellationToken)
    {
        // Build path to _index.revela in source directory
        var indexPath = Path.Combine(SourcePath, gallery.Path, RevelaParser.IndexFileName);
        var basePath = Path.GetDirectoryName(indexPath)!;

        if (!File.Exists(indexPath))
        {
            return (null, new Dictionary<string, string>(), basePath);
        }

        var metadata = await revelaParser.ParseFileAsync(indexPath, cancellationToken);

        // Convert raw body (Markdown) to HTML and set as gallery.Body
        if (metadata.RawBody is not null)
        {
            gallery.Body = markdownService.ToHtml(metadata.RawBody);
        }

        // Always set template (may be null - layout will use default "body/gallery")
        gallery.Template = metadata.Template;

        // Return template info and data sources for custom template processing
        return (metadata.Template, metadata.DataSources, basePath);
    }

    /// <summary>
    /// Gets default data sources from theme extensions for a template.
    /// </summary>
    /// <remarks>
    /// Extensions can define default data sources for their templates in manifest.json.
    /// This allows users to use body templates without explicit data configuration.
    /// </remarks>
    /// <param name="templateKey">Template key (e.g., "statistics/overview")</param>
    /// <returns>Dictionary of default data sources, or empty if none defined</returns>
    private IReadOnlyDictionary<string, string> GetExtensionDataDefaults(string templateKey)
    {
        foreach (var extension in currentExtensions)
        {
            var defaults = extension.GetTemplateDataDefaults(templateKey);
            if (defaults.Count > 0)
            {
                return defaults;
            }
        }

        return new Dictionary<string, string>();
    }

    #endregion

    #region Default Templates

    private static string GetDefaultIndexTemplate() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>{{ site.title }}</title>
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
        </head>
        <body>
            <div class="container py-5">
                <h1>{{ site.title }}</h1>
                <p class="lead">{{ site.description }}</p>

                <div class="row g-4 mt-4">
                {{ for gallery in galleries }}
                    <div class="col-md-4">
                        <div class="card">
                            <div class="card-body">
                                <h5 class="card-title">{{ gallery.name }}</h5>
                                <p class="card-text">{{ gallery.description }}</p>
                                <a href="{{ basepath }}{{ gallery.path }}" class="btn btn-primary">View Gallery</a>
                            </div>
                        </div>
                    </div>
                {{ end }}
                </div>
            </div>
        </body>
        </html>
        """;

    private static string GetDefaultGalleryTemplate() => """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0">
            <title>{{ gallery.title }} - {{ site.title }}</title>
            <link href="https://cdn.jsdelivr.net/npm/bootstrap@5.3.0/dist/css/bootstrap.min.css" rel="stylesheet">
        </head>
        <body>
            <div class="container py-5">
                <h1>{{ gallery.title ?? gallery.name }}</h1>
                <p>{{ gallery.description }}</p>

                <div class="row g-4 mt-4">
                {{ for image in images }}
                    <div class="col-md-4">
                        <picture>
                            <source srcset="{{ basepath }}images/{{ image.file_name }}_1920.webp" type="image/webp">
                            <img src="{{ basepath }}images/{{ image.file_name }}_1920.jpg" class="img-fluid" alt="{{ image.file_name }}">
                        </picture>

                        {{ if image.exif }}
                        <div class="small text-muted mt-2">
                            {{ image.exif.make }} {{ image.exif.model }}<br>
                            f/{{ image.exif.f_number }} ·
                            {{ image.exif.exposure_time }}s ·
                            ISO {{ image.exif.iso }}
                        </div>
                        {{ end }}
                    </div>
                {{ end }}
                </div>

                <a href="{{ basepath }}" class="btn btn-secondary mt-4">Back to Home</a>
            </div>
        </body>
        </html>
        """;

    #endregion

    #region Private Methods - Data Sources

    /// <summary>
    /// Resolved data sources from the data: frontmatter field.
    /// </summary>
    /// <param name="dataSources">Dictionary of variable name → source (JSON file or $built-in)</param>
    /// <param name="basePath">Base path for resolving relative file paths</param>
    /// <param name="projectPath">Project root path for resolving source folder</param>
    /// <param name="allGalleries">All galleries in the site</param>
    /// <param name="localImages">Images in the current gallery folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of resolved data (variable name → value)</returns>
    private static async Task<Dictionary<string, object?>> ResolveDataSourcesAsync(
        IReadOnlyDictionary<string, string> dataSources,
        string basePath,
        string projectPath,
        IReadOnlyList<Gallery> allGalleries,
        IReadOnlyList<Image> localImages,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<string, object?>();

        foreach (var (variableName, source) in dataSources)
        {
            var value = await ResolveSingleDataSourceAsync(
                source,
                basePath,
                projectPath,
                allGalleries,
                localImages,
                cancellationToken);

            if (value is not null)
            {
                result[variableName] = value;
            }
        }

        return result;
    }

    /// <summary>
    /// Resolves a single data source to its value.
    /// </summary>
    private static async Task<object?> ResolveSingleDataSourceAsync(
        string source,
        string basePath,
        string projectPath,
        IReadOnlyList<Gallery> allGalleries,
        IReadOnlyList<Image> localImages,
        CancellationToken cancellationToken)
    {
        // Handle built-in data sources (prefixed with $)
        if (source.StartsWith('$'))
        {
            return source.ToUpperInvariant() switch
            {
                "$GALLERIES" => allGalleries,
                "$IMAGES" => localImages,
                _ => null
            };
        }

        // Handle JSON file references (plugin-generated data from .cache directory)
        if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(
                Path.Combine(projectPath, ProjectPaths.Source),
                basePath);
            var cachePath = Path.Combine(projectPath, ProjectPaths.Cache, relativePath, source);

            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
                var jsonElement = JsonSerializer.Deserialize<JsonElement>(json);
                return ConvertJsonElement(jsonElement);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a JsonElement to Scriban-compatible types.
    /// </summary>
    /// <remarks>
    /// Scriban cannot access properties on JsonElement directly.
    /// This method converts JSON to Dictionary/List structures that Scriban can traverse.
    /// </remarks>
    private static object? ConvertJsonElement(JsonElement element)
    {
#pragma warning disable IDE0072 // Populate switch - we handle all known values explicitly with a fallback
        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject()
                .ToDictionary(p => p.Name, p => ConvertJsonElement(p.Value)),
            JsonValueKind.Array => element.EnumerateArray()
                .Select(ConvertJsonElement)
                .ToList(),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null, // Handles Null, Undefined, and any future values
        };
#pragma warning restore IDE0072
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} pages")]
    private static partial void LogPagesGenerated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger, Exception exception);

    #endregion
}
