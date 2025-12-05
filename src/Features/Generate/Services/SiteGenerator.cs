using System.Text.Json;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
using Spectre.Console;

using NavigationItem = Spectara.Revela.Features.Generate.Models.NavigationItem;

namespace Spectara.Revela.Features.Generate.Services;

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
public sealed class SiteGenerator(
    ContentScanner contentScanner,
    NavigationBuilder navigationBuilder,
    IImageProcessor imageProcessor,
    ITemplateEngine templateEngine,
    IThemeResolver themeResolver)
{
    /// <summary>
    /// Fixed source directory (convention over configuration)
    /// </summary>
    private const string SourceDirectory = "source";

    /// <summary>
    /// Fixed output directory (convention over configuration)
    /// </summary>
    private const string OutputDirectory = "output";

    /// <summary>
    /// Generate complete static site
    /// </summary>
    public async Task GenerateAsync(GenerateOptions options, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Load configuration
        var config = await LoadConfigurationAsync(cancellationToken);

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
        if (options.SkipImages)
        {
            AnsiConsole.MarkupLine("[yellow]Skipping image processing (HTML only mode)[/]\n");
            processedImages = CreatePlaceholderImages(content.Images);
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
        await RenderSiteAsync(siteModel, theme, cancellationToken);
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
    /// Load configuration from project.json and site.json
    /// </summary>
    private static async Task<RevelaConfig> LoadConfigurationAsync(CancellationToken cancellationToken)
    {
        var projectPath = "project.json";
        var sitePath = "site.json";

        if (!File.Exists(projectPath) || !File.Exists(sitePath))
        {
            throw new InvalidOperationException(
                "Configuration files not found. Run 'revela init project' first.");
        }

        var projectJson = await File.ReadAllTextAsync(projectPath, cancellationToken);
        var siteJson = await File.ReadAllTextAsync(sitePath, cancellationToken);

        // Parse JSON (basic implementation - can be enhanced with IOptions pattern)
        var projectData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(projectJson);
        var siteData = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(siteJson);

        return new RevelaConfig
        {
            Project = new ProjectSettings
            {
                Name = GetJsonString(projectData, "title") ?? GetJsonString(projectData, "name") ?? "Revela Site",
                BaseUrl = GetJsonString(projectData, "url") ?? "https://example.com",
                Language = "en"
            },
            Site = new SiteSettings
            {
                Title = GetJsonString(siteData, "title") ?? "Photo Portfolio",
                Author = GetJsonString(siteData, "author"),
                Description = GetJsonString(siteData, "description"),
                Copyright = GetJsonString(siteData, "copyright")
            },
            Theme = new ThemeSettings
            {
                Name = GetJsonString(projectData, "theme") ?? "default"
            },
            Build = new BuildSettings
            {
                Output = "output",
                Images = new ImageSettings
                {
                    Quality = 90,
                    Formats = ["webp", "jpg"],
                    Sizes = [640, 1024, 1280, 1920, 2560]
                }
            }
        };
    }

    /// <summary>
    /// Safely extracts a string value from a JSON dictionary
    /// </summary>
    private static string? GetJsonString(Dictionary<string, JsonElement>? data, string key)
    {
        if (data is null || !data.TryGetValue(key, out var element))
        {
            return null;
        }

        return element.ValueKind == JsonValueKind.String ? element.GetString() : null;
    }

    /// <summary>
    /// Process all images sequentially (NetVips is not thread-safe)
    /// </summary>
    private async Task<List<Image>> ProcessImagesAsync(
        IReadOnlyList<SourceImage> sourceImages,
        CancellationToken cancellationToken)
    {
        var processedImages = new List<Image>();
        var outputImagesDirectory = Path.Combine(OutputDirectory, "images");
        var cacheDirectory = Path.Combine(OutputDirectory, ".cache");

        // Progress tracking
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
                var task = ctx.AddTask("[green]Processing images[/]", maxValue: sourceImages.Count);

                // Process images SEQUENTIALLY
                // NetVips/libvips has global state (codec instances, thread pools, caches) that is NOT thread-safe
                // Even processing different images in parallel causes "out of order read" errors
                // This is the same issue you encountered in Bash with ImageMagick
                foreach (var sourceImage in sourceImages)
                {
                    var image = await imageProcessor.ProcessImageAsync(
                        sourceImage.SourcePath,
                        new ImageProcessingOptions
                        {
                            Quality = 90,
                            Formats = ["webp", "jpg"],
                            Sizes = [640, 1024, 1280, 1920],
                            OutputDirectory = outputImagesDirectory,
                            CacheDirectory = cacheDirectory
                        },
                        cancellationToken);

                    processedImages.Add(image);
                    task.Increment(1);
                }
            });

        return processedImages;
    }

    /// <summary>
    /// Create placeholder images when skipping image processing
    /// </summary>
    /// <remarks>
    /// In HTML-only mode (--skip-images), we still need Image objects for templates.
    /// These placeholders reference expected output paths so templates render correctly.
    /// Actual image files must already exist (from a previous full build).
    /// </remarks>
    private static List<Image> CreatePlaceholderImages(IReadOnlyList<SourceImage> sourceImages)
    {
        var placeholders = new List<Image>();

        foreach (var source in sourceImages)
        {
            // Create placeholder with expected paths
            var fileName = Path.GetFileNameWithoutExtension(source.SourcePath);
            var placeholder = new Image
            {
                SourcePath = source.SourcePath,
                FileName = fileName,
                Width = 0,  // Unknown without processing
                Height = 0,
                // Standard sizes and formats (matching ProcessImagesAsync defaults)
                Variants = CreatePlaceholderVariants(fileName)
            };

            placeholders.Add(placeholder);
        }

        return placeholders;
    }

    /// <summary>
    /// Create placeholder variants for expected sizes and formats
    /// </summary>
    /// <remarks>
    /// Output structure matches Expose theme expectations:
    /// images/{fileName}/{width}.{format}
    /// </remarks>
    private static List<ImageVariant> CreatePlaceholderVariants(string fileName)
    {
        var variants = new List<ImageVariant>();
        int[] sizes = [640, 1024, 1280, 1920];
        string[] formats = ["webp", "jpg"];

        foreach (var size in sizes)
        {
            foreach (var format in formats)
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
    /// Render site templates to HTML
    /// </summary>
    private async Task RenderSiteAsync(
        SiteModel model,
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

        // Render index page (at root, basepath is empty string)
        // Note: For root level, basepath is "" so relative paths like "main.css" work correctly
        var indexNavigation = SetActiveState(model.Navigation, string.Empty);
        var indexHtml = templateEngine.Render(
            indexTemplate,
            new
            {
                site = model.Site,
                gallery = new { title = "Home", body = (string?)null },  // Dummy gallery for index
                galleries = model.Galleries,
                images = model.Images,
                nav_items = indexNavigation,
                basepath = "",  // Root level: empty string, not "./"
                resource_path = "images/"
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
            var basepath = UrlBuilder.CalculateBasePath(gallery.Slug);

            // Set active state for this gallery's path
            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);

            var galleryHtml = templateEngine.Render(
                galleryTemplate,
                new
                {
                    site = model.Site,
                    gallery,
                    images = galleryImages,
                    nav_items = galleryNavigation,
                    basepath,
                    resource_path = $"{basepath}images/"
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
