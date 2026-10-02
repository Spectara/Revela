using System.Diagnostics;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Scriban.Runtime;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Themes;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Filtering;
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

    private const string ContentImageTemplateKey = "partials/contentimage";
    private const string GalleryGridTemplateKey = "partials/gallerygrid";
    private const string PhotoFigureTemplateKey = "partials/photofigure";
    private const string PhotoTemplateKey = "body/photo";

    /// <summary>
    /// Body template of a page without <c>template</c> in its front matter. It shows the folder's
    /// photos as a grid, so those photos get photo pages; custom bodies opt in with <c>[[gallery]]</c>.
    /// </summary>
    private const string DefaultBodyTemplate = "gallery";

    /// <summary>Current theme extensions (set during rendering)</summary>
    private IReadOnlyList<ITheme> currentExtensions = [];

    /// <summary>Gets full path to source directory (supports hot-reload)</summary>
    private string SourcePath => pathResolver.SourcePath;

    /// <summary>Gets full path to output directory (supports hot-reload)</summary>
    private string OutputPath => pathResolver.OutputPath;

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => options.CurrentValue.Images;

    private RenderConfig RenderSettings => options.CurrentValue.Render;

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
            currentExtensions = extensions;

            // Initialize template resolver (scans theme, extensions, local overrides)
            templateResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);
            assetResolver.Initialize(theme, extensions, projectEnvironment.Value.Path);

            var layoutTemplate = LoadTemplate(theme.Manifest.LayoutTemplate);
            if (layoutTemplate is null)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Theme '{config.ThemeName}' is missing its layout template '{theme.Manifest.LayoutTemplate}'. " +
                        "The theme package looks incomplete — try reinstalling it. Run 'revela check' to diagnose your project.",
                    Duration = stopwatch.Elapsed
                };
            }

            var contentImageTemplate = LoadTemplate(ContentImageTemplateKey);
            if (contentImageTemplate is null)
            {
                return new RenderResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Theme '{config.ThemeName}' is missing required template 'Partials/ContentImage.revela'. " +
                        "This template renders ![alt](path) images in Markdown body content.",
                    Duration = stopwatch.Elapsed
                };
            }

            var supportsPhotoPages = theme.Manifest.PhotoViewer?.Supported.Contains(PhotoViewerMode.Page) is true;
            var photoTemplate = supportsPhotoPages ? LoadTemplate(PhotoTemplateKey) : null;
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

            var siteModel = new SiteModel
            {
                // Converted once and shared read-only by all pages, which may render in parallel.
                Site = config.Site is { } site ? JsonScriptConverter.ToScriptValue(site, readOnly: true) : null,
                Galleries = galleries,
                Navigation = navigation,
                Images = [.. galleries.SelectMany(gallery => gallery.Images)]
            };

            // Inline-gallery selections and metadata affect catalog eligibility and must be final
            // before photo pages are built. Preparation is sequential and performs no rendering.
            var allImagesBySourcePath = BuildImageLookup(siteModel);
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

            // Report the real total up front: one page per gallery (the home page included) plus one
            // photo page per eligible image (#77). Photo pages are the bulk of output, so counting them
            // here keeps the progress bar honest instead of hitting 100% after just the galleries.
            progress?.Report(new RenderProgress
            {
                CurrentPage = "Preparing...",
                Rendered = 0,
                Total = galleries.Count + photoPages.Count
            });

            // One engine for the whole run: templates are parsed once and shared by all pages.
            var engine = templateEngineFactory();
            engine.SetStrings(ThemeLocales.Load(theme, extensions, projectEnvironment.Value.Path, config.Project.Language, logger));
            engine.SetImageLookup(allImagesBySourcePath);

            var run = new RenderRun(
                engine,
                siteModel,
                config,
                buildInfo.ToScriptObject(),
                [.. ImageSettings.GetActiveFormats().Keys],
                layoutTemplate,
                contentImageTemplate,
                photoTemplate is not null,
                preparedGalleryMetadata,
                photoMemberships,
                allImagesBySourcePath)
            {
                GalleryGridTemplate = new Lazy<string?>(() => LoadTemplate(GalleryGridTemplateKey)),
                PhotoFigureTemplate = new Lazy<string?>(() => LoadTemplate(PhotoFigureTemplateKey))
            };
            var pageCount = await RenderSiteAsync(run, photoPages, photoTemplate, progress, cancellationToken);

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
                var sitemap = SitemapGenerator.Generate(siteModel, config.Project.BaseUrl, config.Project.BasePath, timeProvider.GetUtcNow().UtcDateTime, photoPages);
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
        var site = siteCoreConfig.CurrentValue;

        // Get theme name from ThemeConfig (IOptions pattern)
        // Fallback to the default theme if not configured
        var themeName = ThemeConfig.ResolveName(themeConfig.CurrentValue.Name);

        return new RenderContext
        {
            Project = new RenderProjectSettings
            {
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
    /// Reconstruct galleries from the unified root tree: the root (home page) first, then
    /// every node with a slug (a page).
    /// </summary>
    private static List<Gallery> ReconstructGalleries(ManifestEntry root)
    {
        var galleries = new List<Gallery> { ReconstructGalleryFromEntry(root) };
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
            Title = entry.Text,
            Description = entry.Description,
            Template = entry.Template,
            DataSources = entry.DataSources,
            Cover = entry.Cover,
            Images = images
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

    /// <summary>
    /// Builds a lookup of all processed images by normalized source path.
    /// </summary>
    /// <remarks>
    /// Includes images from galleries (via <see cref="SiteModel.Images"/>) and
    /// shared images from <c>_images/</c> (via manifest), so Markdown images, covers,
    /// <c>[[photo]]</c> and <c>find_image</c> resolve wherever the image is located.
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
        RenderRun run,
        IReadOnlyList<PhotoPage> photoPages,
        string? photoTemplate,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(OutputPath);

        var pageCount = 0;
        var totalPages = run.Model.Galleries.Count + photoPages.Count;
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = RenderSettings.Parallel ? RenderSettings.MaxDegreeOfParallelism ?? -1 : 1,
            CancellationToken = cancellationToken
        };

        // Gallery pages, the home page included.
        await Parallel.ForEachAsync(run.Model.Galleries, parallelOptions, async (gallery, ct) =>
        {
            var model = await BuildGalleryPageModelAsync(run, gallery, ct);
            var html = run.Engine.Render(run.LayoutTemplate, model);

            var outputDirectory = Path.Combine(OutputPath, gallery.Slug);
            var displayPath = $"{gallery.Slug}index.html";
            Directory.CreateDirectory(outputDirectory);
            WarnIfHtmlTruncated(html, displayPath);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "index.html"), html, ct);

            // Capture the incremented value into a local so the report is parallel-safe.
            var rendered = Interlocked.Increment(ref pageCount);
            progress?.Report(new RenderProgress { CurrentPage = displayPath, Rendered = rendered, Total = totalPages });
        });

        // Photo pages (#77): one canonical page per eligible source image, rendered after the
        // gallery pages so every Gallery.Images list is final. Body/Photo.revela is a standalone
        // document (no site header/overlay-menu).
        if (photoTemplate is not null)
        {
            var photoStylesheets = assetResolver.GetStyleSheets("photo");
            var photoScripts = assetResolver.GetScripts("photo");

            await Parallel.ForEachAsync(photoPages, parallelOptions, async (page, ct) =>
            {
                var relativeBasePath = UrlBuilder.CalculateBasePath($"photo/{page.Slug}/");
                var html = run.Engine.Render(photoTemplate, new Dictionary<string, object?>
                {
                    ["site"] = run.Model.Site,
                    ["photo"] = page.ToScriptObject(),
                    ["image"] = page.Image.ToScriptObject(),
                    ["contexts"] = page.Contexts.ToScriptArray(),
                    ["primary_context"] = page.PrimaryContext.ToScriptObject(),
                    ["basepath"] = CalculateSiteBasePath(run.Config, relativeBasePath),
                    ["assets_basepath"] = CalculateAssetsBasePath(run.Config, relativeBasePath),
                    ["base_url"] = run.Config.Project.BaseUrl,
                    ["image_formats"] = run.ImageFormats,
                    ["revela"] = run.RevelaInfo,
                    ["stylesheets"] = photoStylesheets,
                    ["scripts"] = photoScripts
                });

                var outputDirectory = Path.Combine(OutputPath, "photo", page.Slug.Replace('/', Path.DirectorySeparatorChar));
                var displayPath = $"photo/{page.Slug}/index.html";
                Directory.CreateDirectory(outputDirectory);
                WarnIfHtmlTruncated(html, displayPath);
                await File.WriteAllTextAsync(Path.Combine(outputDirectory, "index.html"), html, ct);

                var rendered = Interlocked.Increment(ref pageCount);
                progress?.Report(new RenderProgress { CurrentPage = displayPath, Rendered = rendered, Total = totalPages });
            });
        }

        await RenderNotFoundPageAsync(run, cancellationToken);

        return pageCount;
    }

    /// <summary>
    /// Builds the layout model of a gallery page — the home page, a folder gallery, a filter
    /// gallery or a text page alike — including its rendered Markdown body and data sources.
    /// </summary>
    private async Task<Dictionary<string, object?>> BuildGalleryPageModelAsync(
        RenderRun run,
        Gallery gallery,
        CancellationToken cancellationToken)
    {
        var metadata = run.PreparedGalleryMetadata[gallery];
        var memberships = run.PhotoMemberships
            .Where(membership => ReferenceEquals(membership.Gallery, gallery))
            .ToList();
        var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
        var basePath = CalculateSiteBasePath(run.Config, relativeBasePath);
        var assetsBasePath = CalculateAssetsBasePath(run.Config, relativeBasePath);

        var renderContentImage = CreateContentImageRenderer(run, assetsBasePath);
        var imageContext = new ContentImageContext(
            run.ImagesBySourcePath,
            gallery.Path,
            renderContentImage,
            CreateGalleryBlockContext(run, metadata, memberships, assetsBasePath, basePath, renderContentImage));
        if (metadata.RawBody is not null)
        {
            gallery.Body = markdownService.ToHtml(metadata.RawBody, imageContext);
        }

        // Page scope for stylesheet filtering: a plugin template like "statistics/overview"
        // scopes to its prefix ("statistics"); the home page defaults to "index", any other
        // page to "gallery".
        var scope = ScopeFromTemplate(metadata.Template, string.IsNullOrEmpty(gallery.Slug) ? "index" : "gallery");
        var baseMembership = memberships.FirstOrDefault(membership => membership.IsBase);

        var model = CreateLayoutModel(
            run,
            gallery.ToScriptObject(),
            gallery.Images.ToScriptArray(),
            baseMembership is null ? [] : BuildOccurrences(baseMembership).ToScriptArray(),
            gallery.Slug,
            basePath,
            assetsBasePath,
            scope,
            metadata.PhotoViewerMode);

        var dataSources = metadata.DataSources.Count > 0
            ? metadata.DataSources
            : GetExtensionDataDefaults(metadata.Template);
        foreach (var (variableName, source) in dataSources)
        {
            var value = await ResolveDataSourceAsync(source, metadata.BasePath, run.Model.Galleries, gallery.Images, cancellationToken);
            if (value is not null)
            {
                model[variableName] = value;
            }
            else if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                LogDataFileMissing(logger, source, gallery.Slug);
            }
        }

        return model;
    }

    /// <summary>
    /// The globals every layout render receives (gallery pages and the 404 page).
    /// </summary>
    private Dictionary<string, object?> CreateLayoutModel(
        RenderRun run,
        ScriptObject gallery,
        ScriptArray images,
        ScriptArray occurrences,
        string currentSlug,
        string basePath,
        string assetsBasePath,
        string scope,
        PhotoViewerMode viewerMode) => new()
        {
            ["site"] = run.Model.Site,
            ["gallery"] = gallery,
            ["images"] = images,
            ["occurrences"] = occurrences,
            ["nav_items"] = SetActiveState(run.Model.Navigation, currentSlug).ToScriptArray(),
            ["basepath"] = basePath,
            ["assets_basepath"] = assetsBasePath,
            ["base_url"] = run.Config.Project.BaseUrl,
            ["image_formats"] = run.ImageFormats,
            ["revela"] = run.RevelaInfo,
            ["stylesheets"] = assetResolver.GetStyleSheets(scope),
            ["scripts"] = GetPageScripts(scope, viewerMode)
        };

    /// <summary>
    /// Renders the theme's <c>Body/NotFound.revela</c> inside the layout to <c>404.html</c> at the output root.
    /// </summary>
    /// <remarks>
    /// Web servers serve this file for arbitrary missing URLs, so the page uses the root-absolute
    /// base path (never a relative one). It is not a content page: it is excluded from navigation,
    /// the sitemap and the page count. Themes without the template simply produce no 404 page, and a
    /// hand-written <c>source/_static/404.html</c> replaces it.
    /// </remarks>
    private async Task RenderNotFoundPageAsync(RenderRun run, CancellationToken cancellationToken)
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

        var basePath = run.Config.Project.BasePath;
        var model = CreateLayoutModel(
            run,
            new ScriptObject { ["template"] = NotFoundScope },
            [],
            [],
            string.Empty,
            basePath,
            CalculateAssetsBasePath(run.Config, basePath),
            NotFoundScope,
            PhotoViewerMode.None);
        model["not_found"] = true;

        var html = run.Engine.Render(run.LayoutTemplate, model);
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
    /// <param name="templateKey">Template key with folder, e.g. "layout", "body/photo" or "partials/contentimage"; a ".revela" suffix is ignored</param>
    /// <returns>Template content or null if not found</returns>
    private string? LoadTemplate(string templateKey)
    {
        using var stream = templateResolver.GetTemplate(templateKey);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

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
                gallery.Template ??= DefaultBodyTemplate;
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

            // A page filter with `sort random` is drawn again on every render, once for the whole
            // run like [[gallery: ... | sort random]] blocks — the order frozen at scan is ignored.
            if (!string.IsNullOrWhiteSpace(metadata.Filter) && FilterService.ParseQuery(metadata.Filter).Sort is { IsRandom: true })
            {
                gallery.Images = GalleryImageResolver.Resolve(
                    imageContentsBySourcePath,
                    metadata.Filter,
                    metadata.Sort,
                    options.CurrentValue.Sorting.Images);
            }

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
                    photoPath => ImagePathResolver.Resolve(photoPath, gallery.Path, imagesBySourcePath));

            gallery.HasInlineGalleries = preparedBlocks.Count > 0;
            gallery.Template = metadata.Template ?? DefaultBodyTemplate;

            if (metadata.Cover is not null)
            {
                gallery.CoverImage = ImagePathResolver.Resolve(metadata.Cover, gallery.Path, imagesBySourcePath);
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
        template is null || template.Equals(DefaultBodyTemplate, StringComparison.OrdinalIgnoreCase);

    private static IReadOnlyList<GalleryImageOccurrence> BuildOccurrences(
        PhotoMembership membership,
        int? bareRenderOrdinal = null)
    {
        var occurrences = new List<GalleryImageOccurrence>(membership.Images.Count);
        var contextLabel = membership.Gallery.Title;
        for (var index = 0; index < membership.Images.Count; index++)
        {
            var image = membership.Images[index];
            occurrences.Add(new GalleryImageOccurrence(
                image,
                membership.ViewerMode.ToValue(),
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


    /// <summary>
    /// Creates a delegate that renders content images via the theme's <c>Partials/ContentImage.revela</c>.
    /// </summary>
    private static Func<Image, string, List<string>?, string> CreateContentImageRenderer(
        RenderRun run,
        string assetsBasePath) =>
        (image, alt, classes) => run.Engine.Render(run.ContentImageTemplate, new Dictionary<string, object?>
        {
            ["image"] = image.ToScriptObject(),
            ["alt"] = alt,
            ["classes"] = classes ?? [],
            ["assets_basepath"] = assetsBasePath,
            ["image_formats"] = run.ImageFormats
        });

    /// <summary>
    /// Creates the page-local context that renders inline-gallery <c>[[gallery]]</c> and
    /// <c>[[photo]]</c> blocks.
    /// </summary>
    /// <remarks>
    /// The grid template (<c>Partials/GalleryGrid.revela</c>) is loaded on first use so that
    /// themes without inline galleries are unaffected. A missing template raises a source-located error.
    /// Photo blocks use the optional <c>Partials/PhotoFigure.revela</c>; themes without it get the
    /// content image wrapped in a link to the photo page.
    /// </remarks>
    private GalleryBlockContext CreateGalleryBlockContext(
        RenderRun run,
        PreparedGalleryMetadata metadata,
        IReadOnlyList<PhotoMembership> memberships,
        string assetsBasePath,
        string basePath,
        Func<Image, string, List<string>?, string> renderContentImage)
    {
        var sourcePath = metadata.SourcePath;

        void ReportWarning(string warning) => LogInlineGalleryWarning(logger, warning);

        string GetGalleryGridTemplate(int line) =>
            run.GalleryGridTemplate.Value
                ?? throw new InvalidOperationException(
                    $"{sourcePath}:{line}: theme is missing required template 'Partials/GalleryGrid.revela'. " +
                    "This template renders [[gallery]] blocks in Markdown body content.");

        string RenderGalleryGrid(PreparedGalleryBlock preparedBlock, int line)
        {
            var membership = memberships.Single(candidate =>
                candidate.PhotoNumber is null && candidate.GridNumber == preparedBlock.GridNumber);
            var occurrences = BuildOccurrences(membership, preparedBlock.BareRenderOrdinal);

            return run.Engine.Render(
                GetGalleryGridTemplate(line),
                new Dictionary<string, object?>
                {
                    ["occurrences"] = occurrences.ToScriptArray(),
                    ["assets_basepath"] = assetsBasePath,
                    ["basepath"] = basePath,
                    ["image_formats"] = run.ImageFormats
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

            if (run.PhotoFigureTemplate.Value is { } photoFigureTemplate)
            {
                return run.Engine.Render(
                    photoFigureTemplate,
                    new Dictionary<string, object?>
                    {
                        ["image"] = image.ToScriptObject(),
                        ["viewer_mode"] = (run.SupportsPhotoPages ? PhotoViewerMode.Page : PhotoViewerMode.None).ToValue(),
                        ["context_id"] = contextId,
                        ["context_label"] = membership?.Gallery.Title,
                        ["occurrence_id"] = occurrenceId,
                        ["assets_basepath"] = assetsBasePath,
                        ["basepath"] = basePath,
                        ["image_formats"] = run.ImageFormats
                    });
            }

            // Fallback for themes without Partials/PhotoFigure.revela: the content image, linked.
            // A file name is no text alternative; without title or description the alt stays empty.
            var alt = new[] { image.Title, image.Description }.FirstOrDefault(text => !string.IsNullOrWhiteSpace(text))
                ?? string.Empty;
            var picture = renderContentImage(image, alt, null);
            if (!run.SupportsPhotoPages)
            {
                return picture;
            }

            var href = ScribanTemplateEngine.PhotoPageUrl(image.Slug, basePath) + (contextId is null ? string.Empty : $"#ctx-{contextId}");
            return $"<a id=\"{ScribanTemplateEngine.HtmlEscape(occurrenceId)}\" href=\"{ScribanTemplateEngine.HtmlEscape(href)}\">{picture}</a>";
        }

        return new GalleryBlockContext(
            sourcePath,
            metadata.PreparedBlocks,
            line => _ = GetGalleryGridTemplate(line),
            RenderGalleryGrid,
            ReportWarning,
            RenderPhoto);
    }

    private string GetGallerySourcePath(Gallery gallery) =>
        Path.Combine(SourcePath, gallery.Path, RevelaParser.IndexFileName);

    /// <summary>
    /// Everything one render run shares across its pages; immutable once rendering starts.
    /// </summary>
    private sealed record RenderRun(
        ITemplateEngine Engine,
        SiteModel Model,
        RenderContext Config,
        ScriptObject RevelaInfo,
        IReadOnlyList<string> ImageFormats,
        string LayoutTemplate,
        string ContentImageTemplate,
        bool SupportsPhotoPages,
        IReadOnlyDictionary<Gallery, PreparedGalleryMetadata> PreparedGalleryMetadata,
        IReadOnlyList<PhotoMembership> PhotoMemberships,
        IReadOnlyDictionary<string, Image> ImagesBySourcePath)
    {
        /// <summary>Gets <c>Partials/GalleryGrid.revela</c>, loaded on first use.</summary>
        public required Lazy<string?> GalleryGridTemplate { get; init; }

        /// <summary>Gets the optional <c>Partials/PhotoFigure.revela</c>, loaded on first use.</summary>
        public required Lazy<string?> PhotoFigureTemplate { get; init; }
    }

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
    private IReadOnlyDictionary<string, string> GetExtensionDataDefaults(string? templateKey)
    {
        if (templateKey is null)
        {
            return new Dictionary<string, string>();
        }

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

    #region Private Methods - Data Sources

    /// <summary>
    /// Resolves one entry of a page's <c>data</c> frontmatter (or an extension's data default).
    /// </summary>
    /// <param name="source">A built-in source (<c>$galleries</c>, <c>$images</c>) or a JSON file name.</param>
    /// <param name="basePath">Folder of the page's <c>_index.revela</c>; JSON files are read from the matching <c>.revela/cache</c> folder.</param>
    /// <param name="allGalleries">All galleries in the site.</param>
    /// <param name="localImages">Images of the current page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The Scriban-native value, or <c>null</c> when the source is unknown or the file is missing.</returns>
    private async Task<object?> ResolveDataSourceAsync(
        string source,
        string basePath,
        IReadOnlyList<Gallery> allGalleries,
        IReadOnlyList<Image> localImages,
        CancellationToken cancellationToken)
    {
        if (source.StartsWith('$'))
        {
            return source.ToUpperInvariant() switch
            {
                "$GALLERIES" => allGalleries.ToScriptArray(),
                "$IMAGES" => localImages.ToScriptArray(),
                _ => null
            };
        }

        // Plugin-generated data from the cache directory (.revela/cache)
        if (source.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
        {
            var relativePath = Path.GetRelativePath(SourcePath, basePath);
            var cachePath = Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Cache, relativePath, source);

            if (File.Exists(cachePath))
            {
                var json = await File.ReadAllTextAsync(cachePath, cancellationToken);
                using var document = JsonDocument.Parse(json);
                return JsonScriptConverter.ToScriptValue(document.RootElement);
            }
        }

        return null;
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






