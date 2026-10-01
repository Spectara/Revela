using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Scriban.Runtime;
using Spectara.Revela.Core.Themes;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Abstractions;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Hosting;
using Spectara.Revela.Sdk.Json;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;
#pragma warning disable IDE0005 // Using directive is unnecessary — namespace holds source-generated extension methods the analyzer cannot see.
using Spectara.Revela.Sdk.TemplateModels;
#pragma warning restore IDE0005
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
    IOptionsMonitor<SiteCoreConfig> siteCoreConfig,
    IOptionsMonitor<GenerateConfig> options,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IBuildInfo buildInfo,
    IArtifactLifecycle artifactLifecycle,
    TimeProvider timeProvider,
    ILogger<RenderService> logger) : IRenderService
{
    /// <summary>Template key, asset scope and output file of the optional theme-provided 404 page.</summary>
    private const string NotFoundTemplateKey = "body/notfound";
    private const string NotFoundScope = "notfound";
    private const string NotFoundFileName = "404.html";

    /// <summary>Current theme extensions (set during rendering)</summary>
    private IReadOnlyList<ITheme> currentExtensions = [];
    private ITheme? currentTheme;
    private IReadOnlyDictionary<string, Image>? currentImageLookup;
    private ThemeStrings currentStrings = ThemeStrings.Empty;

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
        engine.SetStrings(currentStrings);
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

            // Pre-check: site.json is required for templates (provides site.title, etc.).
            // Detect missing file early and return a friendly error instead of letting
            // Scriban fail later with "Cannot get the member site.title for a null object".
            var siteJsonPath = Path.Combine(projectEnvironment.Value.Path, "site.json");
            if (!File.Exists(siteJsonPath))
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        $"site.json not found at '{siteJsonPath}'. " +
                        "Run 'revela config site' to create it from the theme template, " +
                        "or copy site.json from your project source."
                };
            }

            // Pre-check: site.json exists but defines no title. Templates render
            // `{{ site.title }}` as null and Scriban fails with a cryptic
            // "member site.title for a null object". Fail early with a clear message.
            // (SiteCoreConfig.Title is no longer [Required] — the wizard writes site.json
            // incrementally — so the check lives here at the render call site.)
            if (string.IsNullOrWhiteSpace(siteCoreConfig.CurrentValue.Title))
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        "site.json is missing a 'title'. " +
                        "Set \"title\" in site.json (or run 'revela config site'). " +
                        "Run 'revela check' to diagnose your project."
                };
            }

            // Resolve theme and extensions
            var theme = themeRegistry.Resolve(config.ThemeName, projectEnvironment.Value.Path);
            SetTheme(theme);

            // Pre-check: the configured theme is not installed. Without it the renderer
            // cannot resolve the layout/partials the site depends on. Fail early with a
            // clear, actionable message instead of producing broken output.
            if (theme is null)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Theme '{config.ThemeName}' is not installed. " +
                        $"Run 'revela theme install {config.ThemeName}' (or pick an installed theme " +
                        "with 'revela config theme'). Run 'revela check' to diagnose your project."
                };
            }

            // Get theme extensions matching this theme
            var extensions = themeRegistry.GetExtensions(config.ThemeName);
            SetExtensions(extensions);

            // Initialize template resolver (scans theme, extensions, local overrides)
            templateResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);
            assetResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);

            // Theme UI strings for site.language — loaded once per render, shared by all pages.
            currentStrings = ThemeLocales.Load(theme, extensions, projectEnvironment.Value.Path, config.Project.Language, logger);

            var supportsPhotoPages = theme.Manifest.PhotoViewer?.Supported.Contains(PhotoViewerMode.Page) is true;
            var photoTemplate = supportsPhotoPages ? LoadTemplate("body/photo.revela") : null;
            if (supportsPhotoPages && photoTemplate is null)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Theme '{config.ThemeName}' supports the 'page' photo viewer but is missing " +
                        "'Body/Photo.revela'. Add the template or remove 'page' from the theme's supported photo viewers.",
                    Duration = stopwatch.Elapsed
                };
            }

            // Reconstruct galleries and navigation from unified root
            var galleries = ReconstructGalleries(manifestRepository.Root);
            var navigation = ReconstructNavigation(manifestRepository.Root);

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

            // Inline-gallery selections and metadata affect catalog eligibility and must be final
            // before photo pages are built. Preparation is sequential and performs no rendering.
            var allImagesBySourcePath = BuildImageLookup(siteModel);
            currentImageLookup = allImagesBySourcePath;
            var preparedGalleryMetadata = await PrepareGalleryMetadataAsync(
                galleries,
                allImagesBySourcePath,
                manifestRepository.Images,
                theme.Manifest,
                config.ThemeName,
                cancellationToken);
            var photoMemberships = BuildPhotoMemberships(galleries, preparedGalleryMetadata, photoTemplate is not null);

            // Build the photo-page catalog when the theme resolves Body/Photo.revela.
            // Validation runs here — before any output is written — so a route collision
            // aborts the whole render with an actionable, source-listing message.
            IReadOnlyList<PhotoPage> photoPages = [];
            if (photoTemplate is not null)
            {
                photoPages = PhotoPageCatalog.Build(
                    [.. photoMemberships.Where(membership => membership.ViewerMode is PhotoViewerMode.Page)]);

                var photoConflicts = SlugValidator.FindPhotoConflicts(photoPages, galleries);
                if (photoConflicts.Count > 0)
                {
                    return new RenderResult
                    {
                        Success = false,
                        ErrorMessage = SlugValidator.FormatPhotoRouteError(photoConflicts),
                        Duration = stopwatch.Elapsed
                    };
                }
            }

            var invalidationResult = await artifactLifecycle.PrepareToReplaceAsync(
                CoreArtifacts.RenderedSite,
                cancellationToken);
            if (!invalidationResult.Success)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage = invalidationResult.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };
            }

            // Report the real total up front: index (always 1) + sub-galleries + one photo page
            // per eligible image (#77). Photo pages are the bulk of output, so counting them here
            // keeps the progress bar honest instead of hitting 100% after just the galleries.
            progress?.Report(new RenderProgress
            {
                CurrentPage = "Preparing...",
                Rendered = 0,
                Total = 1 + galleries.Count(g => !string.IsNullOrEmpty(g.Path)) + photoPages.Count
            });

            // Render templates
            var engine = CreateAndConfigureEngine();
            var pageCount = await RenderSiteAsync(
                engine,
                siteModel,
                config,
                theme,
                photoPages,
                photoTemplate,
                preparedGalleryMetadata,
                photoMemberships,
                allImagesBySourcePath,
                progress,
                cancellationToken);

            // Post-render work (assets, static files, sitemap) runs after the last page report.
            // Surface a clear label so the final stretch is not a frozen, unlabelled 100% bar.
            progress?.Report(new RenderProgress
            {
                CurrentPage = "Finalizing (assets, sitemap)…",
                Rendered = pageCount,
                Total = pageCount
            });

            // Copy assets (theme, extensions, local overrides)
            await assetResolver.CopyToOutputAsync(OutputPath, cancellationToken);

            // Copy static files (source/_static/ → output/)
            await staticFileService.CopyStaticFilesAsync(SourcePath, OutputPath, cancellationToken);

            // Generate sitemap.xml (requires absolute BaseUrl)
            if (config.Project.BaseUrl is not null)
            {
                var sitemap = SitemapGenerator.Generate(siteModel, config.Project.BaseUrl, config.Project.BasePath, photoPages);
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

        // Site identity core (title, language, …) from site.json. Accessing it here
        // triggers validation — a site.json missing a required 'title' fails at load.
        var site = siteCoreConfig.CurrentValue;

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
                Language = !string.IsNullOrEmpty(site.Language) ? site.Language : "en",
                AssetsBasePath = project.AssetsBasePath,
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
            return JsonDocument.Parse(json, RevelaJsonOptions.LenientDocument).RootElement.Clone();
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
        IReadOnlyList<PhotoPage> photoPages,
        string? photoTemplate,
        IReadOnlyDictionary<Gallery, PreparedGalleryMetadata> preparedGalleryMetadata,
        IReadOnlyList<PhotoMembership> photoMemberships,
        IReadOnlyDictionary<string, Image> allImagesBySourcePath,
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

        // Real total = index (always written once) + sub-galleries + one photo page per eligible
        // image (#77). When a root gallery exists it stands in for the index page, so this matches
        // the final pageCount exactly — the progress bar reaches 100% only when the last page is
        // written, and never overshoots.
        var galleriesToRenderCount = model.Galleries.Count(g => !string.IsNullOrEmpty(g.Path));
        var totalPages = 1 + galleriesToRenderCount + photoPages.Count;

        // Render index page
        progress?.Report(new RenderProgress
        {
            CurrentPage = "index.html",
            Rendered = pageCount,
            Total = totalPages
        });

        var indexNavigation = SetActiveState(model.Navigation, string.Empty);
        var indexBasePath = CalculateSiteBasePath(config, "");
        var indexAssetsBasePath = CalculateAssetsBasePath(config, "");
        var revelaInfo = buildInfo.ToScriptObject();
        var formats = ImageSettings.GetActiveFormats();

        // Get assets from resolver. Stylesheets are resolved per page-type scope so a
        // photo-only, template-only, or plugin-only assets do not bloat unrelated pages.
        // The index scope is resolved below once the root gallery's template is known
        // (a plugin-templated homepage must still get that plugin's CSS).
        var photoStylesheets = assetResolver.GetStyleSheets("photo");
        var photoScripts = assetResolver.GetScripts("photo");

        // Set image lookup on the main engine for the image() template function
        engine.SetImageLookup(allImagesBySourcePath);

        // Load content image template for Markdown body images (theme-customizable)
        var contentImageTemplate = LoadTemplate("partials/contentimage.revela")
            ?? throw new InvalidOperationException(
                "Theme is missing required template 'Partials/ContentImage.revela'. " +
                "This template renders ![alt](path) images in Markdown body content.");

        // Find root gallery (home page) - has empty Path
        var rootGallery = model.Galleries.FirstOrDefault(g => string.IsNullOrEmpty(g.Path));

        // The homepage defaults to the "index" scope, but a plugin-templated homepage
        // (e.g. a calendar landing page with template "calendar/page") must still get
        // that plugin's CSS — derive the scope from the root template like galleries do.
        var indexScope = "index";
        var indexViewerMode = PhotoViewerMode.None;

        // Load root gallery metadata (body content, etc.)
        if (rootGallery is not null)
        {
            var rootAssetsBasePath = CalculateAssetsBasePath(config, "");
            var rootMetadata = preparedGalleryMetadata[rootGallery];
            var rootMemberships = photoMemberships
                .Where(membership => ReferenceEquals(membership.Gallery, rootGallery))
                .ToList();
            var rootContentImageRenderer = CreateContentImageRenderer(
                engine,
                contentImageTemplate,
                rootAssetsBasePath,
                formats.Keys);
            var rootImageContext = new ContentImageContext(
                allImagesBySourcePath,
                rootGallery.Path,
                rootAssetsBasePath,
                formats.Keys,
                rootContentImageRenderer,
                CreateGalleryBlockContext(
                    rootMetadata.PreparedBlocks,
                    engine,
                    rootAssetsBasePath,
                    indexBasePath,
                    formats.Keys,
                    rootMetadata.SourcePath,
                    rootMemberships,
                    rootContentImageRenderer,
                    photoTemplate is not null));
            RenderPreparedGalleryBody(rootGallery, rootMetadata, rootImageContext);
            indexScope = ScopeFromTemplate(rootMetadata.Template, "index");
            indexViewerMode = rootMetadata.PhotoViewerMode;
        }

        var indexStylesheets = assetResolver.GetStyleSheets(indexScope);
        var indexScripts = GetPageScripts(indexScope, indexViewerMode);

        // Use root gallery images if available (may be filtered), otherwise all images
        var indexImages = rootGallery?.Images.Count > 0
            ? rootGallery.Images
            : model.Images;
        var rootBaseMembership = photoMemberships.FirstOrDefault(membership =>
            ReferenceEquals(membership.Gallery, rootGallery) && membership.IsBase);

        var indexHtml = engine.Render(
            indexTemplate,
            new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["gallery"] = rootGallery?.ToScriptObject(),
                ["galleries"] = model.Galleries.ToScriptArray(),
                ["images"] = indexImages.ToScriptArray(),
                ["occurrences"] = rootBaseMembership is null
                    ? Array.Empty<object>()
                    : BuildOccurrences(rootBaseMembership).ToScriptArray(),
                ["nav_items"] = indexNavigation.ToScriptArray(),
                ["basepath"] = indexBasePath,
                ["assets_basepath"] = indexAssetsBasePath,
                ["base_url"] = config.Project.BaseUrl,
                ["image_formats"] = formats.Keys,
                ["revela"] = revelaInfo,
                ["stylesheets"] = indexStylesheets,
                ["scripts"] = indexScripts
            });

        WarnIfHtmlTruncated(indexHtml, "index.html");
        await File.WriteAllTextAsync(
            Path.Combine(OutputPath, "index.html"),
            indexHtml,
            cancellationToken);
        pageCount++;

        var galleriesToRender = model.Galleries.Where(g => !string.IsNullOrEmpty(g.Path)).ToList();

        async Task RenderGalleryAsync(Gallery gallery, ITemplateEngine renderEngine, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var contentAssetsBasePath = CalculateAssetsBasePath(config, UrlBuilder.CalculateBasePath(gallery.Slug));
            var metadata = preparedGalleryMetadata[gallery];
            var galleryMemberships = photoMemberships
                .Where(membership => ReferenceEquals(membership.Gallery, gallery))
                .ToList();
            var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
            var basepath = CalculateSiteBasePath(config, relativeBasePath);
            var galleryContentImageRenderer = CreateContentImageRenderer(
                renderEngine,
                contentImageTemplate,
                contentAssetsBasePath,
                formats.Keys);
            var galleryImageContext = new ContentImageContext(
                allImagesBySourcePath,
                gallery.Path,
                contentAssetsBasePath,
                formats.Keys,
                galleryContentImageRenderer,
                CreateGalleryBlockContext(
                    metadata.PreparedBlocks,
                    renderEngine,
                    contentAssetsBasePath,
                    basepath,
                    formats.Keys,
                    metadata.SourcePath,
                    galleryMemberships,
                    galleryContentImageRenderer,
                    photoTemplate is not null));
            RenderPreparedGalleryBody(gallery, metadata, galleryImageContext);

            // Page scope for stylesheet filtering: a plugin template like
            // "statistics/overview" scopes to its prefix ("statistics"); a plain
            // gallery scopes to "gallery".
            var galleryScope = ScopeFromTemplate(metadata.Template, "gallery");

            var galleryStylesheets = assetResolver.GetStyleSheets(galleryScope);
            var galleryScripts = GetPageScripts(galleryScope, metadata.PhotoViewerMode);

            var galleryImages = gallery.Images.ToList();
            var baseMembership = galleryMemberships.FirstOrDefault(membership => membership.IsBase);

            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);
            var galleryAssetsBasePath = CalculateAssetsBasePath(config, relativeBasePath);

            var effectiveDataSources = metadata.DataSources;
            if (metadata.DataSources.Count == 0 && metadata.Template is not null)
            {
                effectiveDataSources = GetExtensionDataDefaults(metadata.Template);
            }

            var resolvedData = await ResolveDataSourcesAsync(
                effectiveDataSources,
                metadata.BasePath,
                projectEnvironment.Value.Path,
                SourcePath,
                model.Galleries,
                galleryImages,
                ct);

            foreach (var (variableName, source) in effectiveDataSources)
            {
                if (!resolvedData.ContainsKey(variableName)
                    && source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    LogDataFileMissing(logger, source, gallery.Slug);
                }
            }

            // Preserve original markdown body as page_content for custom body templates.
            // Custom templates can use either {{ page_content }} or {{ gallery.body }}.
            var pageContent = gallery.Body ?? string.Empty;

            gallery.Body ??= string.Empty;

            var layoutModel = new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["gallery"] = gallery.ToScriptObject(),
                ["page_content"] = pageContent,
                ["images"] = galleryImages.ToScriptArray(),
                ["occurrences"] = baseMembership is null
                    ? Array.Empty<object>()
                    : BuildOccurrences(baseMembership).ToScriptArray(),
                ["nav_items"] = galleryNavigation.ToScriptArray(),
                ["basepath"] = basepath,
                ["assets_basepath"] = galleryAssetsBasePath,
                ["base_url"] = config.Project.BaseUrl,
                ["image_formats"] = formats.Keys,
                ["revela"] = revelaInfo,
                ["stylesheets"] = galleryStylesheets,
                ["scripts"] = galleryScripts
            };

            foreach (var (key, value) in resolvedData)
            {
                layoutModel[key] = value;
            }

            var galleryHtml = renderEngine.Render(galleryTemplate, layoutModel);

            var galleryOutputPath = Path.Combine(OutputPath, gallery.Slug);
            Directory.CreateDirectory(galleryOutputPath);

            WarnIfHtmlTruncated(galleryHtml, $"{gallery.Slug.TrimEnd('/')}/index.html");
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

        // Photo pages (#77): one canonical page per eligible source image, rendered after the
        // gallery loop so every Gallery.Images list is final. Body/Photo.revela is a standalone
        // document (no site header/overlay-menu). Counted into pageCount alongside index + galleries.
        async Task RenderPhotoPageAsync(PhotoPage page, string template, ITemplateEngine renderEngine, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();

            var pagePath = $"photo/{page.Slug}/";
            var relativeBasePath = UrlBuilder.CalculateBasePath(pagePath);
            var photoBasePath = CalculateSiteBasePath(config, relativeBasePath);
            var photoAssetsBasePath = CalculateAssetsBasePath(config, relativeBasePath);

            var photoModel = new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["photo"] = page.ToScriptObject(),
                ["image"] = page.Image.ToScriptObject(),
                ["contexts"] = page.Contexts.ToScriptArray(),
                ["primary_context"] = page.PrimaryContext.ToScriptObject(),
                ["basepath"] = photoBasePath,
                ["assets_basepath"] = photoAssetsBasePath,
                ["base_url"] = config.Project.BaseUrl,
                ["image_formats"] = formats.Keys,
                ["revela"] = revelaInfo,
                ["stylesheets"] = photoStylesheets,
                ["scripts"] = photoScripts
            };

            var photoHtml = renderEngine.Render(template, photoModel);

            var photoOutputPath = Path.Combine(OutputPath, "photo", page.Slug.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(photoOutputPath);

            WarnIfHtmlTruncated(photoHtml, $"photo/{page.Slug}/index.html");
            await File.WriteAllTextAsync(
                Path.Combine(photoOutputPath, "index.html"),
                photoHtml,
                ct);

            // Capture the incremented value into a local so the report is parallel-safe: the photo
            // loop runs under Parallel.ForEachAsync. Progress<T> marshals callbacks and PagesCommand
            // only assigns task.Value = Rendered, so out-of-order reports are fine.
            var rendered = Interlocked.Increment(ref pageCount);
            progress?.Report(new RenderProgress
            {
                CurrentPage = $"photo/{page.Slug}/index.html",
                Rendered = rendered,
                Total = totalPages
            });
        }

        if (photoTemplate is not null && photoPages.Count > 0)
        {
            if (RenderSettings.Parallel)
            {
                var parallelOptions = new ParallelOptions
                {
                    MaxDegreeOfParallelism = RenderSettings.MaxDegreeOfParallelism ?? -1,
                    CancellationToken = cancellationToken
                };

                await Parallel.ForEachAsync(photoPages, parallelOptions, async (page, ct) =>
                {
                    var renderEngine = CreateAndConfigureEngine();
                    await RenderPhotoPageAsync(page, photoTemplate, renderEngine, ct);
                });
            }
            else
            {
                foreach (var page in photoPages)
                {
                    await RenderPhotoPageAsync(page, photoTemplate, engine, cancellationToken);
                }
            }
        }

        if (layoutTemplate is not null)
        {
            await RenderNotFoundPageAsync(engine, layoutTemplate, model, config, revelaInfo, formats.Keys, cancellationToken);
        }

        return pageCount;
    }

    /// <summary>
    /// Renders the theme's <c>Body/NotFound.revela</c> inside the layout to <c>404.html</c> at the output root.
    /// </summary>
    /// <remarks>
    /// Web servers serve this file for arbitrary missing URLs, so the page uses the root-absolute
    /// base path (never a relative one). It is not a content page: it is excluded from navigation,
    /// the sitemap and the page count. Themes without the template simply produce no 404 page, and a
    /// hand-written <c>source/_static/404.html</c> replaces it.
    /// </remarks>
    private async Task RenderNotFoundPageAsync(
        ITemplateEngine engine,
        string layoutTemplate,
        SiteModel model,
        RenderContext config,
        ScriptObject revelaInfo,
        IEnumerable<string> imageFormats,
        CancellationToken cancellationToken)
    {
        // A hand-written source/_static/404.html wins. Skip rendering instead of relying on the static
        // copy to overwrite: it keeps an existing output file of equal size and newer timestamp.
        if (File.Exists(Path.Combine(SourcePath, ProjectPaths.Static, NotFoundFileName)))
        {
            return;
        }

        // Probe the resolved entries: GetTemplate would log a missing-template warning for themes
        // that simply do not ship a 404 page.
        var hasTemplate = templateResolver.GetAllEntries()
            .Any(entry => entry.Key.Equals(NotFoundTemplateKey, StringComparison.Ordinal));
        if (!hasTemplate)
        {
            LogNotFoundPageSkipped(logger);
            return;
        }

        var basePath = config.Project.BasePath;
        var html = engine.Render(
            layoutTemplate,
            new Dictionary<string, object?>
            {
                ["site"] = model.Site,
                ["gallery"] = new ScriptObject { ["template"] = "notfound" },
                ["galleries"] = model.Galleries.ToScriptArray(),
                ["images"] = Array.Empty<object>(),
                ["occurrences"] = Array.Empty<object>(),
                ["nav_items"] = SetActiveState(model.Navigation, string.Empty).ToScriptArray(),
                ["basepath"] = basePath,
                ["assets_basepath"] = CalculateAssetsBasePath(config, basePath),
                ["base_url"] = config.Project.BaseUrl,
                ["image_formats"] = imageFormats,
                ["revela"] = revelaInfo,
                ["stylesheets"] = assetResolver.GetStyleSheets(NotFoundScope),
                ["scripts"] = assetResolver.GetScripts(NotFoundScope),
                ["not_found"] = true
            });

        WarnIfHtmlTruncated(html, NotFoundFileName);
        await File.WriteAllTextAsync(Path.Combine(OutputPath, NotFoundFileName), html, cancellationToken);
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

    /// <summary>
    /// Derives the asset scope token from a page's template. A namespaced template
    /// like "statistics/overview" scopes to its prefix ("statistics"); a plain
    /// template like "docs" uses its full name. Only a null/empty template uses
    /// <paramref name="fallback"/>.
    /// </summary>
    private static string ScopeFromTemplate(string? template, string fallback)
    {
        if (string.IsNullOrEmpty(template))
        {
            return fallback;
        }

        var slashIndex = template.IndexOf('/', StringComparison.Ordinal);
        return slashIndex > 0 ? template[..slashIndex] : template;
    }

    private IReadOnlyList<string> GetPageScripts(string scope, PhotoViewerMode viewerMode)
    {
        var scripts = assetResolver.GetScripts(scope);
        if (viewerMode is not PhotoViewerMode.Lightbox)
        {
            return scripts;
        }

        return
        [
            .. scripts,
            .. assetResolver.GetScripts("lightbox")
                .Where(script => !scripts.Contains(script, StringComparer.OrdinalIgnoreCase))
        ];
    }

    private static string CalculateAssetsBasePath(RenderContext config, string basepath)
    {
        if (!string.IsNullOrEmpty(config.Project.AssetsBasePath))
        {
            return config.Project.AssetsBasePath;
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
    /// Loads and prepares metadata from every gallery before photo-page catalog construction.
    /// </summary>
    /// <remarks>
    /// Preparation parses inline-gallery blocks and freezes their selected image occurrences.
    /// </remarks>
    private async Task<IReadOnlyDictionary<Gallery, PreparedGalleryMetadata>> PrepareGalleryMetadataAsync(
        IReadOnlyList<Gallery> galleries,
        IReadOnlyDictionary<string, Image> imagesBySourcePath,
        IReadOnlyDictionary<string, ImageContent> imageContentsBySourcePath,
        ThemeManifest themeManifest,
        string themeName,
        CancellationToken cancellationToken)
    {
        var prepared = new Dictionary<Gallery, PreparedGalleryMetadata>();
        foreach (var gallery in galleries)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var sourcePath = GetGallerySourcePath(gallery);
            var basePath = Path.GetDirectoryName(sourcePath)!;
            if (!File.Exists(sourcePath))
            {
                var missingFileViewerMode = PhotoViewerResolver.Resolve(
                    null,
                    themeConfig.CurrentValue.PhotoViewer,
                    themeManifest,
                    sourcePath,
                    themeName);
                gallery.HasInlineGalleries = false;
                prepared.Add(
                    gallery,
                    new PreparedGalleryMetadata(
                        sourcePath,
                        null,
                        gallery.Template,
                        gallery.DataSources,
                        basePath,
                        PreparedGalleryBlocks.Empty,
                        missingFileViewerMode));
                continue;
            }

            var metadata = await revelaParser.ParseFileAsync(sourcePath, cancellationToken);
            var viewerMode = PhotoViewerResolver.Resolve(
                metadata.PhotoViewer,
                themeConfig.CurrentValue.PhotoViewer,
                themeManifest,
                sourcePath,
                themeName);
            var rawBody = metadata.RawBody;
            var preparedBlocks = rawBody is null
                ? PreparedGalleryBlocks.Empty
                : markdownService.PrepareGalleryBlocks(
                    rawBody,
                    sourcePath,
                    gallery.Images,
                    filterExpression => GalleryImageResolver.Resolve(
                        imageContentsBySourcePath,
                        filterExpression,
                        metadata.Sort,
                        options.CurrentValue.Sorting.Images),
                    photoPath => ResolveImageByPath(photoPath, gallery.Path, imagesBySourcePath));

            gallery.HasInlineGalleries = preparedBlocks.Count > 0;
            gallery.Template = metadata.Template;

            if (metadata.Cover is not null)
            {
                gallery.CoverImage = ResolveImageByPath(metadata.Cover, gallery.Path, imagesBySourcePath);
            }

            prepared.Add(
                gallery,
                new PreparedGalleryMetadata(
                    sourcePath,
                    rawBody,
                    metadata.Template,
                    metadata.DataSources,
                    basePath,
                    preparedBlocks,
                    viewerMode));
        }

        return prepared;
    }

    /// <summary>
    /// Builds every photo membership in stable site order: base galleries and filtered grids first,
    /// then <c>[[photo]]</c> blocks.
    /// </summary>
    /// <remarks>
    /// A <c>[[photo]]</c> block always links to a photo page when the theme renders photo pages,
    /// regardless of the page's viewer mode. Page-context blocks add a single-image membership
    /// (no previous/next). <c>| gallery</c> blocks add none, unless the photo has no gallery or
    /// grid membership anywhere — then the page context is used so its photo page has a way back.
    /// </remarks>
    private static IReadOnlyList<PhotoMembership> BuildPhotoMemberships(
        IReadOnlyList<Gallery> galleries,
        IReadOnlyDictionary<Gallery, PreparedGalleryMetadata> preparedGalleryMetadata,
        bool supportsPhotoPages)
    {
        var memberships = new List<PhotoMembership>();
        foreach (var gallery in galleries)
        {
            var metadata = preparedGalleryMetadata[gallery];
            var blocks = metadata.PreparedBlocks.Blocks.Values.ToList();
            var hasBareBlock = blocks.Any(block => block.GridNumber is null && block.Images.Count > 0);
            var hasTrailingGrid = blocks.Count == 0 && IsDefaultGalleryBody(metadata.Template);

            if ((hasBareBlock || hasTrailingGrid) && gallery.Images.Count > 0)
            {
                memberships.Add(new PhotoMembership(gallery, gallery.Images, null, metadata.PhotoViewerMode));
            }

            var filteredBlocks = blocks
                .Where(block => block.GridNumber is not null && block.Images.Count > 0)
                .OrderBy(block => block.GridNumber);
            memberships.AddRange(filteredBlocks.Select(block =>
                new PhotoMembership(gallery, block.Images, block.GridNumber, metadata.PhotoViewerMode)));
        }

        if (!supportsPhotoPages)
        {
            return memberships;
        }

        var photosWithGalleryContext = memberships
            .Where(membership => membership.ViewerMode is PhotoViewerMode.Page)
            .SelectMany(membership => membership.Images)
            .Select(image => PhotoPageCatalog.NormalizeSourcePath(image.SourcePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var gallery in galleries)
        {
            var photoBlocks = preparedGalleryMetadata[gallery].PreparedBlocks.Photos.Values
                .Where(photo => photo.Image is { Sizes.Count: > 0 })
                .OrderBy(photo => photo.PhotoNumber);
            foreach (var photo in photoBlocks)
            {
                var image = photo.Image!;
                if (photo.UsesPageContext ||
                    !photosWithGalleryContext.Contains(PhotoPageCatalog.NormalizeSourcePath(image.SourcePath)))
                {
                    memberships.Add(new PhotoMembership(gallery, [image], null, PhotoViewerMode.Page, photo.PhotoNumber));
                }
            }
        }

        return memberships;
    }

    private static bool IsDefaultGalleryBody(string? template) =>
        template is null
        || template.Equals("gallery", StringComparison.OrdinalIgnoreCase)
        || template.Equals("body/gallery", StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<GalleryImageOccurrence> BuildOccurrences(
        PhotoMembership membership,
        int? bareRenderOrdinal = null)
    {
        var occurrences = new List<GalleryImageOccurrence>(membership.Images.Count);
        var contextLabel = GalleryLabel(membership.Gallery);
        for (var index = 0; index < membership.Images.Count; index++)
        {
            var image = membership.Images[index];
            occurrences.Add(new GalleryImageOccurrence(
                image,
                ViewerModeValue(membership.ViewerMode),
                PhotoPageCatalog.ContextId(membership),
                contextLabel,
                OccurrenceId(image.Slug, membership.GridNumber, bareRenderOrdinal),
                index > 0
                    ? OccurrenceId(membership.Images[index - 1].Slug, membership.GridNumber, bareRenderOrdinal)
                    : null,
                index < membership.Images.Count - 1
                    ? OccurrenceId(membership.Images[index + 1].Slug, membership.GridNumber, bareRenderOrdinal)
                    : null));
        }

        return occurrences;
    }

    // The first bare [[gallery]] block keeps the plain anchor that photo pages link back to;
    // only repeats of the same block need a prefix to keep ids unique on the page.
    private static string OccurrenceId(string imageSlug, int? gridNumber, int? bareRenderOrdinal) =>
        bareRenderOrdinal is null or 1
            ? PhotoPageCatalog.Anchor(imageSlug, gridNumber)
            : $"bare-{bareRenderOrdinal.Value}-{PhotoPageCatalog.Anchor(imageSlug, null)}";

    private static string ViewerModeValue(PhotoViewerMode viewerMode) => viewerMode switch
    {
        PhotoViewerMode.Page => "page",
        PhotoViewerMode.Lightbox => "lightbox",
        PhotoViewerMode.None => "none",
        _ => throw new ArgumentOutOfRangeException(nameof(viewerMode), viewerMode, null)
    };

    private void RenderPreparedGalleryBody(
        Gallery gallery,
        PreparedGalleryMetadata metadata,
        ContentImageContext imageContext)
    {
        if (metadata.RawBody is not null)
        {
            gallery.Body = markdownService.ToHtml(metadata.RawBody, imageContext);
        }
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
    private static Image? ResolveImageByPath(
        string imagePath,
        string galleryPath,
        IReadOnlyDictionary<string, Image> imagesBySourcePath)
    {
        var normalizedPath = imagePath.Replace('\\', '/');

        // 1. Gallery-local
        if (!string.IsNullOrEmpty(galleryPath))
        {
            var localPath = $"{galleryPath}/{normalizedPath}";
            if (imagesBySourcePath.TryGetValue(localPath, out var localImage))
            {
                return localImage;
            }
        }

        // 2. Shared images: _images/{path}
        var sharedPath = $"{ProjectPaths.SharedImages}/{normalizedPath}";
        if (imagesBySourcePath.TryGetValue(sharedPath, out var sharedImage))
        {
            return sharedImage;
        }

        // 3. Exact match
        if (imagesBySourcePath.TryGetValue(normalizedPath, out var exactImage))
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
        string assetsBasePath,
        IEnumerable<string> imageFormats) =>
        (image, alt, classes) => engine.Render(template, new Dictionary<string, object?>
        {
            ["image"] = image.ToScriptObject(),
            ["alt"] = alt,
            ["classes"] = classes ?? [],
            ["assets_basepath"] = assetsBasePath,
            ["image_formats"] = imageFormats
        });

    /// <summary>
    /// Creates the page-local context that renders inline-gallery <c>[[gallery]]</c> and
    /// <c>[[photo]]</c> blocks.
    /// </summary>
    /// <remarks>
    /// The grid template (<c>Partials/GalleryGrid.revela</c>) is loaded lazily on first use so that
    /// themes without inline galleries are unaffected. A missing template raises a source-located error.
    /// Photo blocks use the optional <c>Partials/PhotoFigure.revela</c>; themes without it get the
    /// content image wrapped in a link to the photo page.
    /// </remarks>
    private GalleryBlockContext CreateGalleryBlockContext(
        PreparedGalleryBlocks preparedBlocks,
        ITemplateEngine engine,
        string assetsBasePath,
        string basePath,
        IEnumerable<string> imageFormats,
        string sourcePath,
        IReadOnlyList<PhotoMembership> memberships,
        Func<Image, string, List<string>?, string> renderContentImage,
        bool supportsPhotoPages)
    {
        string? galleryGridTemplate = null;
        string? photoFigureTemplate = null;
        var photoFigureTemplateLoaded = false;

        void ReportWarning(string warning) => LogInlineGalleryWarning(logger, warning);

        string GetGalleryGridTemplate(int line) =>
            galleryGridTemplate ??= LoadTemplate("partials/gallerygrid.revela")
                ?? throw new InvalidOperationException(
                    $"{sourcePath}:{line}: theme is missing required template 'Partials/GalleryGrid.revela'. " +
                    "This template renders [[gallery]] blocks in Markdown body content.");

        string RenderGalleryGrid(PreparedGalleryBlock preparedBlock, int line)
        {
            var membership = memberships.Single(candidate =>
                candidate.PhotoNumber is null && candidate.GridNumber == preparedBlock.GridNumber);
            var occurrences = BuildOccurrences(membership, preparedBlock.BareRenderOrdinal);

            return engine.Render(
                GetGalleryGridTemplate(line),
                new Dictionary<string, object?>
                {
                    ["occurrences"] = occurrences.ToScriptArray(),
                    ["assets_basepath"] = assetsBasePath,
                    ["basepath"] = basePath,
                    ["image_formats"] = imageFormats
                });
        }

        string RenderPhoto(PreparedPhotoBlock photo, int line)
        {
            var image = photo.Image
                ?? throw new InvalidOperationException($"{sourcePath}:{line}: unresolved photo cannot be rendered.");
            var membership = memberships.FirstOrDefault(candidate => candidate.PhotoNumber == photo.PhotoNumber);
            if (!photo.UsesPageContext && membership is not null)
            {
                ReportWarning(
                    $"{sourcePath}:{line}: photo '{photo.ImagePath}' is in no gallery with photo pages; " +
                    "its photo page returns to this page instead.");
            }

            var contextId = membership is null ? null : PhotoPageCatalog.ContextId(membership);
            var occurrenceId = PhotoPageCatalog.PhotoAnchor(image.Slug, photo.PhotoNumber);

            if (!photoFigureTemplateLoaded)
            {
                photoFigureTemplate = LoadTemplate("partials/photofigure.revela");
                photoFigureTemplateLoaded = true;
            }

            if (photoFigureTemplate is not null)
            {
                return engine.Render(
                    photoFigureTemplate,
                    new Dictionary<string, object?>
                    {
                        ["image"] = image.ToScriptObject(),
                        ["viewer_mode"] = supportsPhotoPages ? "page" : "none",
                        ["context_id"] = contextId,
                        ["context_label"] = membership is null ? null : GalleryLabel(membership.Gallery),
                        ["occurrence_id"] = occurrenceId,
                        ["assets_basepath"] = assetsBasePath,
                        ["basepath"] = basePath,
                        ["image_formats"] = imageFormats
                    });
            }

            // Fallback for themes without Partials/PhotoFigure.revela: the content image, linked.
            var alt = new[] { image.Title, image.Description }.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? image.Id;
            var picture = renderContentImage(image, alt, null);
            if (!supportsPhotoPages)
            {
                return picture;
            }

            var href = ScribanTemplateEngine.PageUrl(image, basePath) + (contextId is null ? string.Empty : $"#ctx-{contextId}");
            return $"<a id=\"{ScribanTemplateEngine.HtmlEscape(occurrenceId)}\" href=\"{ScribanTemplateEngine.HtmlEscape(href)}\">{picture}</a>";
        }

        return new GalleryBlockContext(
            sourcePath,
            preparedBlocks,
            line => _ = GetGalleryGridTemplate(line),
            RenderGalleryGrid,
            ReportWarning,
            RenderPhoto);
    }

    private static string GalleryLabel(Gallery gallery) =>
        !string.IsNullOrWhiteSpace(gallery.Title) ? gallery.Title : gallery.Name;

    private string GetGallerySourcePath(Gallery gallery) =>
        Path.Combine(SourcePath, gallery.Path, RevelaParser.IndexFileName);

    private sealed record PreparedGalleryMetadata(
        string SourcePath,
        string? RawBody,
        string? Template,
        IReadOnlyDictionary<string, string> DataSources,
        string BasePath,
        PreparedGalleryBlocks PreparedBlocks,
        PhotoViewerMode PhotoViewerMode);

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

    /// <summary>
    /// Sanity-check rendered HTML for silent truncation. Emits a warning if the
    /// output does not end with the expected closing tag, which can indicate that
    /// the template engine aborted rendering mid-document (e.g. exceeded a
    /// runtime output limit).
    /// </summary>
    private void WarnIfHtmlTruncated(string html, string outputPath)
    {
        if (string.IsNullOrEmpty(html))
        {
            LogHtmlTruncated(logger, outputPath, 0, "(empty)");
            return;
        }

        var trimmed = html.AsSpan().TrimEnd();
        if (!trimmed.EndsWith("</html>", StringComparison.OrdinalIgnoreCase))
        {
            var tailLength = Math.Min(60, trimmed.Length);
            var tail = trimmed[^tailLength..].ToString();
            LogHtmlTruncated(logger, outputPath, html.Length, tail);
        }
    }

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} pages")]
    private static partial void LogPagesGenerated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Information, Message = "Sitemap skipped: set 'baseUrl' in project.json for sitemap.xml generation")]
    private static partial void LogSitemapSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Debug, Message = "404.html skipped: the theme provides no 'Body/NotFound.revela'")]
    private static partial void LogNotFoundPageSkipped(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Rendered HTML for {OutputPath} appears truncated ({Length} bytes, ends with: {Tail}). The page does not close with </html> — check for template runtime limits or rendering errors.")]
    private static partial void LogHtmlTruncated(ILogger logger, string outputPath, int length, string tail);

    [LoggerMessage(Level = LogLevel.Warning, Message = "{Warning}")]
    private static partial void LogInlineGalleryWarning(ILogger logger, string warning);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Data file '{DataFile}' for page '/{PagePath}' is missing, so the page renders without it. Run the generate step that creates it (for example 'revela generate statistics') or 'revela generate all'")]
    private static partial void LogDataFileMissing(ILogger logger, string dataFile, string pagePath);

    #endregion
}






