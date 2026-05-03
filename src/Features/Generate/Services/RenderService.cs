using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Scriban.Runtime;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Service for template rendering and HTML page generation.
/// </summary>
/// <remarks>
/// <para>
/// Renders HTML pages from manifest data using Scriban templates.
/// Copies theme assets to output directory.
/// </para>
/// </remarks>
internal sealed partial class RenderService(
    Func<ITemplateEngine> templateEngineFactory,
    IThemeRegistry themeRegistry,
    ITemplateResolver templateResolver,
    IAssetResolver assetResolver,
    IStaticFileService staticFileService,
    IManifestRepository manifestRepository,
    RevelaParser revelaParser,
    IMarkdownService markdownService,
    IOptions<ProjectEnvironment> projectEnvironment,
    IPathResolver pathResolver,
    IOptionsMonitor<ProjectConfig> projectConfig,
    IOptionsMonitor<GenerateConfig> options,
    IOptionsMonitor<ThemeConfig> themeConfig,
    TimeProvider timeProvider,
    ILogger<RenderService> logger) : IRenderService
{
    /// <summary>Current theme extensions (set during rendering)</summary>
    private IReadOnlyList<ITheme> currentExtensions = [];
    private ITheme? currentTheme;
    private IReadOnlyDictionary<string, Image>? currentImageLookup;

    /// <summary>Gets full path to source directory (supports hot-reload)</summary>
    private string SourcePath => pathResolver.SourcePath;

    /// <summary>Gets full path to output directory (supports hot-reload)</summary>
    private string OutputPath => pathResolver.OutputPath;

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => options.CurrentValue.Images;

    private RenderConfig RenderSettings => options.CurrentValue.Render;

    private ITemplateEngine CreateAndConfigureEngine()
    {
        var engine = templateEngineFactory();
        engine.SetTheme(currentTheme);
        engine.SetExtensions(currentExtensions);
        if (currentImageLookup is not null)
        {
            engine.SetImageLookup(currentImageLookup);
        }

        return engine;
    }

    /// <inheritdoc />
    public void SetTheme(ITheme? theme) => currentTheme = theme;

    /// <inheritdoc />
    public void SetExtensions(IReadOnlyList<ITheme> extensions) => currentExtensions = extensions;

    /// <inheritdoc />
    public string Render(string templateContent, object model)
    {
        var engine = CreateAndConfigureEngine();
        return engine.Render(templateContent, model);
    }

    /// <inheritdoc />
    public async Task<string> RenderFileAsync(
        string templatePath,
        object model,
        CancellationToken cancellationToken = default)
    {
        var engine = CreateAndConfigureEngine();
        return await engine.RenderFileAsync(templatePath, model, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<RenderResult> RenderAsync(
        IProgress<RenderProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

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
            var config = await LoadConfigurationAsync(cancellationToken);

            // Resolve theme and extensions
            var theme = themeRegistry.Resolve(config.ThemeName, projectEnvironment.Value.Path);
            SetTheme(theme);

            // Get theme extensions matching this theme
            var extensions = themeRegistry.GetExtensions(config.ThemeName);
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
                BuildDate = timeProvider.GetUtcNow().UtcDateTime
            };

            // Render templates
            var engine = CreateAndConfigureEngine();
            var pageCount = await RenderSiteAsync(engine, siteModel, config, theme, progress, cancellationToken);

            // Copy assets (theme, extensions, local overrides)
            if (theme is not null)
            {
                await assetResolver.CopyToOutputAsync(OutputPath, cancellationToken);
            }

            // Copy static files (source/_static/ → output/)
            await staticFileService.CopyStaticFilesAsync(SourcePath, OutputPath, cancellationToken);

            // Generate sitemap.xml (requires absolute BaseUrl)
            if (config.Project.BaseUrl is not null)
            {
                var sitemap = SitemapGenerator.Generate(siteModel, config.Project.BaseUrl, config.Project.BasePath);
                await File.WriteAllTextAsync(
                    Path.Combine(OutputPath, "sitemap.xml"),
                    sitemap,
                    cancellationToken);
            }
            else
            {
                LogSitemapSkipped(logger);
            }

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

    private async Task<RenderContext> LoadConfigurationAsync(CancellationToken cancellationToken)
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
                BaseUrl = project.BaseUrl?.ToString().TrimEnd('/'),
                Language = !string.IsNullOrEmpty(project.Language) ? project.Language : "en",
                ImageBasePath = project.ImageBasePath,
                BasePath = NormalizeBasePath(project.BasePath)
            },
            Site = await LoadSiteJsonAsync(cancellationToken),
            ThemeName = themeName
        };
    }

    /// <summary>
    /// Load site.json directly as JsonElement for dynamic template access.
    /// </summary>
    /// <returns>JsonElement with site properties, or null if site.json doesn't exist.</returns>
    private async Task<JsonElement?> LoadSiteJsonAsync(CancellationToken cancellationToken)
    {
        var siteJsonPath = Path.Combine(projectEnvironment.Value.Path, "site.json");
        if (!File.Exists(siteJsonPath))
        {
            return null;
        }

        try
        {
            var json = await File.ReadAllTextAsync(siteJsonPath, cancellationToken);
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

    /// <summary>
    /// Builds a lookup of all processed images by normalized source path.
    /// </summary>
    /// <remarks>
    /// Includes images from galleries (via <see cref="SiteModel.Images"/>) and
    /// shared images from <c>_images/</c> (via manifest). This enables the
    /// <see cref="ContentImageExtension"/> to resolve image references from
    /// Markdown body content regardless of where the image is located.
    /// </remarks>
    private Dictionary<string, Image> BuildImageLookup(SiteModel model)
    {
        var lookup = new Dictionary<string, Image>(StringComparer.OrdinalIgnoreCase);

        // Add gallery images (SourcePath = relative path like "Landscapes/sunset.jpg")
        foreach (var image in model.Images)
        {
            var key = image.SourcePath.Replace('\\', '/');
            lookup.TryAdd(key, image);
        }

        // Add shared images from manifest (_images/*)
        // These aren't in any gallery but are processed by the image pipeline
        foreach (var (sourcePath, imageContent) in manifestRepository.Images)
        {
            if (!lookup.ContainsKey(sourcePath))
            {
                lookup[sourcePath] = Image.FromManifestEntry(sourcePath, imageContent);
            }
        }

        return lookup;
    }

    #endregion

    #region Private Methods - Rendering

    private async Task<int> RenderSiteAsync(
        ITemplateEngine engine,
        SiteModel model,
        RenderContext config,
        ITheme? theme,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(OutputPath);

        var manifest = theme?.Manifest;
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
        var themeVariables = theme?.Manifest.Variables ?? new Dictionary<string, string>();
        var formats = ImageSettings.GetActiveFormats();

        // Get assets from resolver
        var stylesheets = assetResolver.GetStyleSheets();
        var scripts = assetResolver.GetScripts();

        // Build lookup of ALL processed images by source path (for content image resolution).
        // Includes gallery images (from model) and shared _images (from manifest).
        var allImagesBySourcePath = BuildImageLookup(model);
        currentImageLookup = allImagesBySourcePath;

        // Set image lookup on the main engine for the image() template function
        engine.SetImageLookup(allImagesBySourcePath);

        // Load content image template for Markdown body images (theme-customizable)
        var contentImageTemplate = LoadTemplate("partials/contentimage.revela")
            ?? throw new InvalidOperationException(
                "Theme is missing required template 'Partials/ContentImage.revela'. " +
                "This template renders ![alt](path) images in Markdown body content.");

        // Find root gallery (home page) - has empty Path
        var rootGallery = model.Galleries.FirstOrDefault(g => string.IsNullOrEmpty(g.Path));

        // Load root gallery metadata (body content, etc.)
        if (rootGallery is not null)
        {
            var rootImageBasePath = CalculateImageBasePath(config, "");
            var rootImageContext = new ContentImageContext(
                allImagesBySourcePath,
                rootGallery.Path,
                rootImageBasePath,
                formats.Keys,
                CreateContentImageRenderer(engine, contentImageTemplate, rootImageBasePath, formats.Keys));
            var (_, _, _) = await LoadGalleryMetadataAsync(rootGallery, rootImageContext, cancellationToken);
        }

        // Use root gallery images if available (may be filtered), otherwise all images
        var indexImages = rootGallery?.Images.Count > 0
            ? rootGallery.Images
            : model.Images;

        var indexHtml = engine.Render(
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

        var galleriesToRender = model.Galleries.Where(g => !string.IsNullOrEmpty(g.Path)).ToList();
        pageCount++; // index rendered

        async Task RenderGalleryAsync(Gallery gallery, ITemplateEngine renderEngine, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            // Load metadata from _index.md at render time (not stored in manifest)
            var galleryImageContext = new ContentImageContext(
                allImagesBySourcePath,
                gallery.Path,
                CalculateImageBasePath(config, UrlBuilder.CalculateBasePath(gallery.Slug)),
                formats.Keys,
                CreateContentImageRenderer(renderEngine, contentImageTemplate, CalculateImageBasePath(config, UrlBuilder.CalculateBasePath(gallery.Slug)), formats.Keys));
            var (customTemplate, dataSources, metadataBasePath) = await LoadGalleryMetadataAsync(gallery, galleryImageContext, ct);

            var galleryImages = gallery.Images.ToList();

            var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
            var basepath = CalculateSiteBasePath(config, relativeBasePath);
            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);
            var galleryImageBasePath = CalculateImageBasePath(config, relativeBasePath);

            var effectiveDataSources = dataSources;
            if (dataSources.Count == 0 && customTemplate is not null)
            {
                effectiveDataSources = GetExtensionDataDefaults(customTemplate);
            }

            var resolvedData = await ResolveDataSourcesAsync(
                effectiveDataSources,
                metadataBasePath,
                projectEnvironment.Value.Path,
                SourcePath,
                model.Galleries,
                galleryImages,
                ct);

            // Preserve original markdown body as page_content for custom body templates.
            // Custom templates can use either {{ page_content }} or {{ gallery.body }}.
            var pageContent = gallery.Body ?? string.Empty;

            gallery.Body ??= string.Empty;

            var layoutModel = new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["gallery"] = gallery,
                ["page_content"] = pageContent,
                ["images"] = galleryImages,
                ["nav_items"] = galleryNavigation,
                ["basepath"] = basepath,
                ["image_basepath"] = galleryImageBasePath,
                ["image_formats"] = formats.Keys,
                ["theme"] = themeVariables,
                ["stylesheets"] = stylesheets,
                ["scripts"] = scripts
            };

            foreach (var (key, value) in resolvedData)
            {
                layoutModel[key] = value;
            }

            var galleryHtml = renderEngine.Render(galleryTemplate, layoutModel);

            var galleryOutputPath = Path.Combine(OutputPath, gallery.Slug);
            Directory.CreateDirectory(galleryOutputPath);

            await File.WriteAllTextAsync(
                Path.Combine(galleryOutputPath, "index.html"),
                galleryHtml,
                ct);

            var rendered = Interlocked.Increment(ref pageCount);
            progress?.Report(new RenderProgress
            {
                CurrentPage = $"{gallery.Slug.TrimEnd('/')}​/index.html",
                Rendered = rendered,
                Total = totalPages
            });
        }

        if (RenderSettings.Parallel)
        {
            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = RenderSettings.MaxDegreeOfParallelism ?? -1,
                CancellationToken = cancellationToken
            };

            await Parallel.ForEachAsync(galleriesToRender, parallelOptions, async (gallery, ct) =>
            {
                var renderEngine = CreateAndConfigureEngine();
                await RenderGalleryAsync(gallery, renderEngine, ct);
            });
        }
        else
        {
            foreach (var gallery in galleriesToRender)
            {
                await RenderGalleryAsync(gallery, engine, cancellationToken);
            }
        }

        return pageCount;
    }

    private static List<NavigationItem> SetActiveState(
        IReadOnlyList<NavigationItem> items,
        string currentPath) => [.. items.Select(item => SetActiveStateRecursive(item, currentPath))];

    private static NavigationItem SetActiveStateRecursive(NavigationItem item, string currentPath)
    {
        // Current = exact match (this is the current page)
        var isCurrent = !string.IsNullOrEmpty(item.Url) &&
                        !string.IsNullOrEmpty(currentPath) &&
                        currentPath.Equals(item.Url, StringComparison.OrdinalIgnoreCase);

        // Active = in path (this item or a child is current)
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
            Current = isCurrent,
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
        ContentImageContext imageContext,
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

        // Convert raw body (Markdown) to HTML with content image resolution
        if (metadata.RawBody is not null)
        {
            gallery.Body = markdownService.ToHtml(metadata.RawBody, imageContext);
        }

        // Resolve cover image path to Image object using same lookup as content images
        if (metadata.Cover is not null)
        {
            gallery.CoverImage = ResolveImageByPath(metadata.Cover, imageContext);
        }

        // Always set template (may be null - layout will use default "body/gallery")
        gallery.Template = metadata.Template;

        // Return template info and data sources for custom template processing
        return (metadata.Template, metadata.DataSources, basePath);
    }

    /// <summary>
    /// Resolves an image path (from frontmatter) to an Image object.
    /// </summary>
    /// <remarks>
    /// Uses the same 3-step lookup as content images in Markdown:
    /// <list type="number">
    /// <item>Gallery-local: <c>{GalleryPath}/{path}</c></item>
    /// <item>Shared images: <c>_images/{path}</c></item>
    /// <item>Exact match: <c>{path}</c> as-is</item>
    /// </list>
    /// </remarks>
    private static Image? ResolveImageByPath(string imagePath, ContentImageContext imageContext)
    {
        var normalizedPath = imagePath.Replace('\\', '/');

        // 1. Gallery-local
        if (!string.IsNullOrEmpty(imageContext.GalleryPath))
        {
            var localPath = $"{imageContext.GalleryPath}/{normalizedPath}";
            if (imageContext.ImagesBySourcePath.TryGetValue(localPath, out var localImage))
            {
                return localImage;
            }
        }

        // 2. Shared images: _images/{path}
        var sharedPath = $"{ProjectPaths.SharedImages}/{normalizedPath}";
        if (imageContext.ImagesBySourcePath.TryGetValue(sharedPath, out var sharedImage))
        {
            return sharedImage;
        }

        // 3. Exact match
        if (imageContext.ImagesBySourcePath.TryGetValue(normalizedPath, out var exactImage))
        {
            return exactImage;
        }

        return null;
    }

    /// <summary>
    /// Creates a delegate that renders content images via a theme template.
    /// </summary>
    /// <returns>Render delegate, or null if no content image template is available.</returns>
    private static Func<Image, string, List<string>?, string> CreateContentImageRenderer(
        ITemplateEngine engine,
        string template,
        string imageBasePath,
        IEnumerable<string> imageFormats) =>
        (image, alt, classes) => engine.Render(template, new Dictionary<string, object?>
        {
            ["image"] = image,
            ["alt"] = alt,
            ["classes"] = classes ?? [],
            ["image_basepath"] = imageBasePath,
            ["image_formats"] = imageFormats
        });

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
    /// <param name="sourcePath">Resolved source directory path</param>
    /// <param name="allGalleries">All galleries in the site</param>
    /// <param name="localImages">Images in the current gallery folder</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary of resolved data (variable name → value)</returns>
    private static async Task<Dictionary<string, object?>> ResolveDataSourcesAsync(
        IReadOnlyDictionary<string, string> dataSources,
        string basePath,
        string projectPath,
        string sourcePath,
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
                sourcePath,
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
        string sourcePath,
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
                sourcePath,
                basePath);
            var cachePath = Path.Combine(projectPath, ProjectPaths.Cache, relativePath, source);

            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
                using var document = JsonDocument.Parse(json);
                return ConvertJsonElement(document.RootElement);
            }
        }

        return null;
    }

    /// <summary>
    /// Converts a JsonElement to Scriban-compatible types.
    /// </summary>
    /// <remarks>
    /// Scriban cannot access properties on plain Dictionary or JsonElement directly.
    /// This method converts JSON to ScriptObject/ScriptArray structures that Scriban
    /// can traverse with dot-notation (e.g., statistics.cameras).
    /// </remarks>
    private static object? ConvertJsonElement(JsonElement element)
    {
#pragma warning disable IDE0072 // Populate switch - we handle all known values explicitly with a fallback
        return element.ValueKind switch
        {
            JsonValueKind.Object => ConvertJsonObject(element),
            JsonValueKind.Array => ConvertJsonArray(element),
            JsonValueKind.String => element.GetString(),
            JsonValueKind.Number when element.TryGetInt64(out var l) => l,
            JsonValueKind.Number => element.GetDouble(),
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => null, // Handles Null, Undefined, and any future values
        };
#pragma warning restore IDE0072
    }

    /// <summary>
    /// Converts a JSON object to a ScriptObject for Scriban template access.
    /// </summary>
    private static ScriptObject ConvertJsonObject(JsonElement element)
    {
        var obj = new ScriptObject();
        foreach (var prop in element.EnumerateObject())
        {
            obj[prop.Name] = ConvertJsonElement(prop.Value);
        }

        return obj;
    }

    /// <summary>
    /// Converts a JSON array to a ScriptArray for Scriban template access.
    /// </summary>
    private static ScriptArray ConvertJsonArray(JsonElement element)
    {
        var arr = new ScriptArray();
        foreach (var item in element.EnumerateArray())
        {
            arr.Add(ConvertJsonElement(item));
        }

        return arr;
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} pages")]
    private static partial void LogPagesGenerated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sitemap skipped: set 'baseUrl' in project.json for sitemap.xml generation")]
    private static partial void LogSitemapSkipped(ILogger logger);

    #endregion
}






