using Microsoft.Extensions.Configuration;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Models;
using Spectre.Console;

using NavigationItem = Spectara.Revela.Commands.Generate.Models.NavigationItem;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Orchestrates site generation workflow
/// </summary>
/// <remarks>
/// Generation workflow:
/// 1. Load configuration (project.json, site.json)
/// 2. Resolve theme (local → plugin → default)
/// 3. Scan content directory (ContentScanner)
/// 4. Build navigation tree (NavigationBuilder)
/// 5. Process images in parallel (IImageProcessor) - unless SkipImages
/// 6. Build site model (galleries, navigation)
/// 7. Render templates (ITemplateEngine)
/// 8. Write output files and copy theme assets
/// </remarks>
public sealed partial class SiteGenerator(
    ContentScanner contentScanner,
    NavigationBuilder navigationBuilder,
    IImageProcessor imageProcessor,
    ITemplateEngine templateEngine,
    IThemeResolver themeResolver,
    ImageManifestService manifestService,
    IConfiguration configuration,
    ILogger<SiteGenerator> logger)
{
    /// <summary>
    /// Fixed source directory (convention over configuration)
    /// </summary>
    private const string SourceDirectory = "source";

    /// <summary>
    /// Output directory for generated site (convention over configuration)
    /// </summary>
    private const string OutputDirectory = "output";

    /// <summary>
    /// Image output directory within output folder
    /// </summary>
    private const string ImageDirectory = "images";

    /// <summary>
    /// Generate complete static site
    /// </summary>
    public async Task GenerateAsync(GenerateOptions options, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Load configuration from IConfiguration
        var config = LoadConfiguration();

        // Step 2: Resolve theme
        AnsiConsole.MarkupLine("[yellow]Resolving theme...[/]");
        var theme = themeResolver.Resolve(config.Theme.Name, Environment.CurrentDirectory);
        if (theme is null)
        {
            AnsiConsole.MarkupLine($"[yellow]Theme '{config.Theme.Name}' not found, using default templates[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine($"[green]Using theme:[/] {theme.Metadata.Name} v{theme.Metadata.Version}\n");
        }

        // Set theme for template engine (enables partial loading via include)
        templateEngine.SetTheme(theme);

        // Step 3: Scan content
        AnsiConsole.MarkupLine("[yellow]Scanning content...[/]");
        var content = await contentScanner.ScanAsync(SourceDirectory, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Found {content.Images.Count} images in {content.Galleries.Count} galleries[/]\n");

        if (content.Images.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No images found in source directory.[/]");
            AnsiConsole.MarkupLine("[dim]Add images to the source directory and try again.[/]");
            return;
        }

        // Step 4: Build navigation tree (once, used for all pages)
        AnsiConsole.MarkupLine("[yellow]Building navigation...[/]");
        var navigation = await navigationBuilder.BuildAsync(SourceDirectory, cancellationToken: cancellationToken);
        AnsiConsole.MarkupLine($"[green]Navigation built[/]\n");

        // Step 5: Process images (unless skipped)
        List<Image> processedImages;
        var projectDirectory = Environment.CurrentDirectory;
        if (options.SkipImages)
        {
            AnsiConsole.MarkupLine("[yellow]Skipping image processing (using cached data)...[/]");
            processedImages = await CreateImagesFromManifestAsync(content.Images, projectDirectory, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Loaded {processedImages.Count} images from cache[/]\n");
        }
        else
        {
            AnsiConsole.MarkupLine("[yellow]Processing images...[/]");
            processedImages = await ProcessImagesAsync(content.Images, cancellationToken);
            AnsiConsole.MarkupLine($"[green]Processed {processedImages.Count} images[/]\n");
        }

        // Step 6: Build site model
        var siteModel = BuildSiteModel(config, content, processedImages, navigation);

        // Step 7: Render templates
        AnsiConsole.MarkupLine("[yellow]Rendering templates...[/]");
        await RenderSiteAsync(siteModel, config, theme, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Site rendered successfully[/]\n");

        // Step 8: Copy theme assets
        if (theme is not null)
        {
            AnsiConsole.MarkupLine("[yellow]Copying theme assets...[/]");
            await CopyThemeAssetsAsync(theme, cancellationToken);
            AnsiConsole.MarkupLine("[green]Theme assets copied[/]\n");
        }

        var elapsed = DateTime.UtcNow - startTime;
        AnsiConsole.MarkupLine($"[dim]Generation completed in {elapsed.TotalSeconds:F2}s[/]");
    }

    /// <summary>
    /// Load configuration from IConfiguration (project.json + site.json merged)
    /// </summary>
    /// <remarks>
    /// Configuration is loaded via IConfiguration pipeline in Program.cs.
    /// This maps the flat JSON keys to RevelaConfig structure:
    /// - project.json: name, url, theme, basePath, imageBasePath, generate.*
    /// - site.json: title, author, description, copyright
    /// </remarks>
    private RevelaConfig LoadConfiguration()
    {
        // Get generate settings (properly bound via IConfiguration)
        var generateSettings = new GenerateSettings();
        configuration.GetSection("generate").Bind(generateSettings);

        return new RevelaConfig
        {
            Project = new ProjectSettings
            {
                Name = configuration["title"] ?? configuration["name"] ?? "Revela Site",
                BaseUrl = configuration["url"] ?? "https://example.com",
                Language = configuration["language"] ?? "en",
                ImageBasePath = configuration["imageBasePath"],
                BasePath = NormalizeBasePath(configuration["basePath"])
            },
            Site = new SiteSettings
            {
                Title = configuration["title"] ?? "Photo Portfolio",
                Author = configuration["author"],
                Description = configuration["description"],
                Copyright = configuration["copyright"]
            },
            Theme = new ThemeSettings
            {
                Name = configuration["theme"] ?? "default"
            },
            Generate = generateSettings
        };
    }

    /// <summary>
    /// Normalize basePath to ensure it starts and ends with "/"
    /// </summary>
    /// <remarks>
    /// Examples:
    /// - null or empty → "/"
    /// - "photos" → "/photos/"
    /// - "/photos" → "/photos/"
    /// - "photos/" → "/photos/"
    /// - "/photos/" → "/photos/" (unchanged)
    /// </remarks>
    private static string NormalizeBasePath(string? basePath)
    {
        if (string.IsNullOrWhiteSpace(basePath))
        {
            return "/";
        }

        var normalized = basePath.Trim();

        // Ensure starts with /
        if (!normalized.StartsWith('/'))
        {
            normalized = "/" + normalized;
        }

        // Ensure ends with /
        if (!normalized.EndsWith('/'))
        {
            normalized += "/";
        }

        return normalized;
    }

    // Image processing configuration (TODO: read from project.json)
    private static readonly int[] ImageSizes = [640, 1024, 1280, 1920];
    private static readonly string[] ImageFormats = ["webp", "jpg"];
    private const int ImageQuality = 90;

    /// <summary>
    /// Process all images with manifest-based caching
    /// </summary>
    /// <remarks>
    /// Uses manifest to skip unchanged images:
    /// 1. Load existing manifest
    /// 2. Check config hash (force full rebuild if sizes/formats changed)
    /// 3. For each image: compare source hash, skip if unchanged
    /// 4. Save updated manifest
    /// </remarks>
    private async Task<List<Image>> ProcessImagesAsync(
        IReadOnlyList<SourceImage> sourceImages,
        CancellationToken cancellationToken)
    {
        var processedImages = new List<Image>();
        var outputImagesDirectory = Path.Combine(OutputDirectory, ImageDirectory);
        var cacheDirectory = Path.Combine(Environment.CurrentDirectory, ".cache");

        // Load existing manifest
        var manifest = await manifestService.LoadAsync(cacheDirectory, cancellationToken);

        // Check if config changed (forces full rebuild)
        var configHash = ImageManifestService.ComputeConfigHash(ImageSizes, ImageFormats, ImageQuality);
        var configChanged = ImageManifestService.ConfigChanged(manifest, configHash);

        if (configChanged && manifest.Images.Count > 0)
        {
            LogConfigChanged(logger);
            manifest = new ImageManifest(); // Start fresh
        }

        manifest.Meta.ConfigHash = configHash;

        // Build set of current source paths for orphan detection (normalized to forward slashes)
        var currentSourcePaths = sourceImages
            .Select(s => s.RelativePath.Replace('\\', '/'))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Remove orphaned entries
        var orphans = manifestService.RemoveOrphans(manifest, currentSourcePaths);

        // Count images needing processing
        var imagesToProcess = new List<(SourceImage Source, string Hash, string ManifestKey)>();
        var cachedImages = new List<(SourceImage Source, ImageManifestEntry Entry)>();

        foreach (var sourceImage in sourceImages)
        {
            var sourceHash = ImageManifestService.ComputeSourceHash(sourceImage.SourcePath);
            var manifestKey = sourceImage.RelativePath.Replace('\\', '/');

            if (ImageManifestService.NeedsProcessing(manifest, manifestKey, sourceHash))
            {
                imagesToProcess.Add((sourceImage, sourceHash, manifestKey));
            }
            else
            {
                var entry = manifest.Images[manifestKey];
                cachedImages.Add((sourceImage, entry));
            }
        }

        // Report cache hits
        if (cachedImages.Count > 0)
        {
            LogCacheHits(logger, cachedImages.Count, sourceImages.Count);
        }

        // Add cached images to result
        foreach (var (source, entry) in cachedImages)
        {
            processedImages.Add(Image.FromManifestEntry(source.SourcePath, entry));
        }

        // Process only changed images
        if (imagesToProcess.Count > 0)
        {
            await AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async ctx =>
                {
                    var task = ctx.AddTask("[green]Processing images[/]", maxValue: imagesToProcess.Count);

                    foreach (var (sourceImage, sourceHash, manifestKey) in imagesToProcess)
                    {
                        var image = await imageProcessor.ProcessImageAsync(
                            sourceImage.SourcePath,
                            new ImageProcessingOptions
                            {
                                Quality = ImageQuality,
                                Formats = ImageFormats,
                                Sizes = ImageSizes,
                                OutputDirectory = outputImagesDirectory,
                                CacheDirectory = cacheDirectory
                            },
                            cancellationToken);

                        // Update manifest with new entry
                        manifest.Images[manifestKey] = new ImageManifestEntry
                        {
                            Hash = sourceHash,
                            OriginalWidth = image.Width,
                            OriginalHeight = image.Height,
                            GeneratedSizes = image.AvailableSizes.Count > 0
                                ? image.AvailableSizes
                                : [.. ImageSizes.Where(s => s <= Math.Max(image.Width, image.Height))],
                            GeneratedFormats = image.AvailableFormats.Count > 0
                                ? image.AvailableFormats
                                : [.. ImageFormats],
                            OutputPath = image.FileName,
                            FileSize = image.FileSize,
                            DateTaken = image.DateTaken,
                            Exif = image.Exif
                        };

                        processedImages.Add(image);
                        task.Increment(1);
                    }
                });
        }

        // Save updated manifest
        await manifestService.SaveAsync(manifest, cacheDirectory, cancellationToken);

        return processedImages;
    }

    /// <summary>
    /// Create images from manifest when skipping image processing
    /// </summary>
    /// <remarks>
    /// In HTML-only mode (--skip-images), we use cached data from manifest.
    /// This provides accurate dimensions and available sizes for templates.
    /// Falls back to placeholder data if manifest entry is missing.
    /// </remarks>
    private async Task<List<Image>> CreateImagesFromManifestAsync(
        IReadOnlyList<SourceImage> sourceImages,
        string projectPath,
        CancellationToken cancellationToken)
    {
        var images = new List<Image>();
        var cacheDirectory = Path.Combine(projectPath, ".cache");
        var manifest = await manifestService.LoadAsync(cacheDirectory, cancellationToken);

        foreach (var source in sourceImages)
        {
            // Use relative path normalized to forward slashes for consistent keys
            var manifestKey = source.RelativePath.Replace('\\', '/');

            if (manifest.Images.TryGetValue(manifestKey, out var entry))
            {
                // Use cached data from manifest
                images.Add(Image.FromManifestEntry(source.SourcePath, entry));
            }
            else
            {
                // Fallback: Create placeholder (no previous build exists)
                var fileName = Path.GetFileNameWithoutExtension(source.SourcePath);
                images.Add(CreatePlaceholderImage(source.SourcePath, fileName));
            }
        }

        return images;
    }

    /// <summary>
    /// Create placeholder image when no manifest entry exists
    /// </summary>
    private static Image CreatePlaceholderImage(string sourcePath, string fileName)
    {
        return new Image
        {
            SourcePath = sourcePath,
            FileName = fileName,
            Width = 0,  // Unknown without processing
            Height = 0,
            Variants = CreatePlaceholderVariants(fileName),
            AvailableSizes = [.. ImageSizes],
            AvailableFormats = [.. ImageFormats]
        };
    }

    /// <summary>
    /// Create placeholder variants for expected sizes and formats
    /// </summary>
    private static List<ImageVariant> CreatePlaceholderVariants(string fileName)
    {
        var variants = new List<ImageVariant>();

        foreach (var size in ImageSizes)
        {
            foreach (var format in ImageFormats)
            {
                variants.Add(new ImageVariant
                {
                    Width = size,
                    Height = 0,  // Unknown without processing
                    Format = format,
                    Path = $"images/{fileName}/{size}.{format}",
                    Size = 0
                });
            }
        }

        return variants;
    }

    /// <summary>
    /// Build site model for template rendering
    /// </summary>
    private static SiteModel BuildSiteModel(
        RevelaConfig config,
        ContentTree content,
        List<Image> processedImages,
        IReadOnlyList<NavigationItem> navigation) =>
        new()
        {
            Site = config.Site,
            Project = config.Project,
            Galleries = [.. content.Galleries],
            Images = processedImages,
            Navigation = navigation,
            BuildDate = DateTime.UtcNow
        };

    /// <summary>
    /// Calculate basepath for site assets and navigation links
    /// </summary>
    /// <remarks>
    /// For root hosting (BasePath = "/"):
    /// - Root level: "" → "main.css", "events/"
    /// - Gallery level: "../" → "../main.css", "../events/"
    ///
    /// For subdirectory hosting (BasePath = "/photos/"):
    /// - Root level: "/photos/" → "/photos/main.css"
    /// - Gallery level: "/photos/" → "/photos/main.css" (absolute path)
    /// </remarks>
    private static string CalculateSiteBasePath(RevelaConfig config, string relativeBasePath)
    {
        // For root hosting ("/"), use relative paths
        if (config.Project.BasePath == "/")
        {
            return relativeBasePath;
        }

        // For subdirectory hosting, use absolute path with BasePath prefix
        // This ensures links work correctly regardless of nesting depth
        return config.Project.BasePath;
    }

    /// <summary>
    /// Calculate image_basepath for templates
    /// </summary>
    /// <remarks>
    /// Priority:
    /// 1. If ImageBasePath is configured → use absolute URL (e.g., "https://cdn.example.com/images/")
    /// 2. Default → relative path to images/ folder (e.g., "images/" or "../images/")
    /// </remarks>
    private static string CalculateImageBasePath(RevelaConfig config, string basepath)
    {
        // Absolute CDN URL configured → use directly
        if (!string.IsNullOrEmpty(config.Project.ImageBasePath))
        {
            return config.Project.ImageBasePath;
        }

        // Default: relative path to images/ folder
        return $"{basepath}images/";
    }

    /// <summary>
    /// Render site templates to HTML
    /// </summary>
    private async Task RenderSiteAsync(
        SiteModel model,
        RevelaConfig config,
        IThemePlugin? theme,
        CancellationToken cancellationToken)
    {
        // Ensure output directory exists
        Directory.CreateDirectory(OutputDirectory);

        // Load templates from theme or use defaults
        // Try theme's layout template first, then fallback to specific templates, then defaults
        var manifest = theme?.GetManifest();
        var layoutTemplate = manifest is not null
            ? LoadTemplate(theme, manifest.LayoutTemplate)
            : null;

        var indexTemplate = layoutTemplate
            ?? LoadTemplate(theme, "index.html")
            ?? GetDefaultIndexTemplate();
        var galleryTemplate = layoutTemplate
            ?? LoadTemplate(theme, "gallery.html")
            ?? GetDefaultGalleryTemplate();

        // Render index page (at root level)
        var indexNavigation = SetActiveState(model.Navigation, string.Empty);
        var indexBasePath = CalculateSiteBasePath(config, "");
        var indexImageBasePath = CalculateImageBasePath(config, "");
        var indexHtml = templateEngine.Render(
            indexTemplate,
            new
            {
                site = model.Site,
                gallery = new { title = "Home", body = (string?)null },  // Dummy gallery for index
                galleries = model.Galleries,
                images = model.Images,
                nav_items = indexNavigation,
                basepath = indexBasePath,
                image_basepath = indexImageBasePath
            });

        await File.WriteAllTextAsync(
            Path.Combine(OutputDirectory, "index.html"),
            indexHtml,
            cancellationToken);

        // Render gallery pages
        foreach (var gallery in model.Galleries)
        {
            var galleryImages = model.Images
                .Where(img => img.SourcePath.Contains(gallery.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            // Calculate basepath based on gallery depth (use Slug for URL structure)
            var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
            var basepath = CalculateSiteBasePath(config, relativeBasePath);

            // Set active state for this gallery's path
            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);

            // Calculate image_basepath (handles CDN URLs and separate output)
            var galleryImageBasePath = CalculateImageBasePath(config, relativeBasePath);

            var galleryHtml = templateEngine.Render(
                galleryTemplate,
                new
                {
                    site = model.Site,
                    gallery,
                    images = galleryImages,
                    nav_items = galleryNavigation,
                    basepath,
                    image_basepath = galleryImageBasePath
                });

            // Use Slug for output path (URL-friendly directory names)
            var galleryOutputPath = Path.Combine(OutputDirectory, gallery.Slug);
            Directory.CreateDirectory(galleryOutputPath);

            await File.WriteAllTextAsync(
                Path.Combine(galleryOutputPath, "index.html"),
                galleryHtml,
                cancellationToken);
        }
    }

    /// <summary>
    /// Creates a copy of navigation with active state set for the current path
    /// </summary>
    /// <param name="items">Original navigation items</param>
    /// <param name="currentPath">Current page path (e.g., "events/2024/")</param>
    /// <returns>Navigation items with Active property set</returns>
    private static List<NavigationItem> SetActiveState(
        IReadOnlyList<NavigationItem> items,
        string currentPath)
    {
        return [.. items.Select(item => SetActiveStateRecursive(item, currentPath))];
    }

    /// <summary>
    /// Recursively sets active state on a navigation item and its children
    /// </summary>
    private static NavigationItem SetActiveStateRecursive(NavigationItem item, string currentPath)
    {
        // Check if this item is active (matches current path or is an ancestor)
        var isActive = !string.IsNullOrEmpty(item.Url) &&
                      !string.IsNullOrEmpty(currentPath) &&
                      (currentPath.Equals(item.Url, StringComparison.OrdinalIgnoreCase) ||
                       currentPath.StartsWith(item.Url, StringComparison.OrdinalIgnoreCase));

        // Recursively process children
        List<NavigationItem> activeChildren = [.. item.Children.Select(c => SetActiveStateRecursive(c, currentPath))];

        return new NavigationItem
        {
            Text = item.Text,
            Url = item.Url,
            Description = item.Description,
            Active = isActive,
            Hidden = item.Hidden,
            Children = activeChildren
        };
    }

    /// <summary>
    /// Load template from theme
    /// </summary>
    /// <param name="theme">Theme to load from (can be null)</param>
    /// <param name="templateName">Template filename (e.g., "index.html")</param>
    /// <returns>Template content or null if not found</returns>
    private static string? LoadTemplate(IThemePlugin? theme, string templateName)
    {
        if (theme is null)
        {
            return null;
        }

        using var stream = theme.GetFile(templateName);
        if (stream is null)
        {
            return null;
        }

        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    /// <summary>
    /// Copy theme assets (CSS, JS, fonts, images) to output directory
    /// </summary>
    private static async Task CopyThemeAssetsAsync(IThemePlugin theme, CancellationToken cancellationToken)
    {
        var manifest = theme.GetManifest();

        foreach (var assetPath in manifest.Assets)
        {
            using var stream = theme.GetFile(assetPath);
            if (stream is null)
            {
                continue;
            }

            var outputPath = Path.Combine(OutputDirectory, assetPath);
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            await using var fileStream = File.Create(outputPath);
            await stream.CopyToAsync(fileStream, cancellationToken);
        }
    }

    /// <summary>
    /// Default index template (minimal Bootstrap-based layout)
    /// </summary>
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

    /// <summary>
    /// Default gallery template
    /// </summary>
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

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Config changed, rebuilding all images")]
    private static partial void LogConfigChanged(ILogger logger);

    [LoggerMessage(Level = LogLevel.Information, Message = "Using cached data for {CacheHits}/{Total} images")]
    private static partial void LogCacheHits(ILogger logger, int cacheHits, int total);

    #endregion
}

/// <summary>
/// Site model for template rendering
/// </summary>
public sealed class SiteModel
{
    public required SiteSettings Site { get; init; }
    public required ProjectSettings Project { get; init; }
    public required IReadOnlyList<Gallery> Galleries { get; init; }
    public required IReadOnlyList<Image> Images { get; init; }
    public required IReadOnlyList<NavigationItem> Navigation { get; init; }
    public DateTime BuildDate { get; init; }
}
