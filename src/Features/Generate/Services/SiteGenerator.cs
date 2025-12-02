using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Models;
using Spectre.Console;
using System.Text.Json;

namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Orchestrates site generation workflow
/// </summary>
/// <remarks>
/// Generation workflow:
/// 1. Load configuration (project.json, site.json)
/// 2. Scan content directory (ContentScanner)
/// 3. Process images in parallel (IImageProcessor)
/// 4. Build site model (galleries, navigation)
/// 5. Render templates (ITemplateEngine)
/// 6. Write output files
/// </remarks>
public sealed class SiteGenerator(
    ContentScanner contentScanner,
    IImageProcessor imageProcessor,
    ITemplateEngine templateEngine)
{
    /// <summary>
    /// Generate complete static site
    /// </summary>
    public async Task GenerateAsync(GenerateOptions options, CancellationToken cancellationToken = default)
    {
        var startTime = DateTime.UtcNow;

        // Step 1: Load configuration
        var config = await LoadConfigurationAsync(cancellationToken);

        // Step 2: Scan content
        AnsiConsole.MarkupLine("[yellow]Scanning content...[/]");
        var content = await contentScanner.ScanAsync(options.SourceDirectory, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Found {content.Images.Count} images in {content.Galleries.Count} galleries[/]\n");

        if (content.Images.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No images found in source directory.[/]");
            AnsiConsole.MarkupLine("[dim]Add images to the source directory and try again.[/]");
            return;
        }

        // Step 3: Process images (parallel with progress)
        AnsiConsole.MarkupLine("[yellow]Processing images...[/]");
        var processedImages = await ProcessImagesAsync(content.Images, options, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Processed {processedImages.Count} images[/]\n");

        // Step 4: Build site model
        var siteModel = BuildSiteModel(config, content, processedImages);

        // Step 5: Render templates
        AnsiConsole.MarkupLine("[yellow]Rendering templates...[/]");
        await RenderSiteAsync(siteModel, options.OutputDirectory, cancellationToken);
        AnsiConsole.MarkupLine($"[green]Site rendered successfully[/]\n");

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
                Name = projectData?.GetValueOrDefault("name").GetString() ?? "Revela Site",
                BaseUrl = projectData?.GetValueOrDefault("url").GetString() ?? "https://example.com",
                Language = "en"
            },
            Site = new SiteSettings
            {
                Title = siteData?.GetValueOrDefault("title").GetString() ?? "Photo Portfolio",
                Author = siteData?.GetValueOrDefault("author").GetString(),
                Description = siteData?.GetValueOrDefault("description").GetString(),
                Copyright = siteData?.GetValueOrDefault("copyright").GetString()
            },
            Theme = new ThemeSettings
            {
                Name = projectData?.GetValueOrDefault("theme").GetString() ?? "default"
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
    /// Process all images sequentially (NetVips is not thread-safe)
    /// </summary>
    private async Task<List<Image>> ProcessImagesAsync(
        IReadOnlyList<SourceImage> sourceImages,
        GenerateOptions options,
        CancellationToken cancellationToken)
    {
        var processedImages = new List<Image>();
        var outputImagesDirectory = Path.Combine(options.OutputDirectory, "images");
        var cacheDirectory = Path.Combine(options.OutputDirectory, ".cache");

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
    /// Build site model for template rendering
    /// </summary>
    private static SiteModel BuildSiteModel(
        RevelaConfig config,
        ContentTree content,
        List<Image> processedImages) =>
        new()
        {
            Site = config.Site,
            Project = config.Project,
            Galleries = [.. content.Galleries],
            Images = processedImages,
            BuildDate = DateTime.UtcNow
        };

    /// <summary>
    /// Render site templates to HTML
    /// </summary>
    private async Task RenderSiteAsync(
        SiteModel model,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);

        // Render index page
        var indexHtml = templateEngine.Render(
            GetDefaultIndexTemplate(),
            new { site = model.Site, galleries = model.Galleries, images = model.Images });

        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "index.html"),
            indexHtml,
            cancellationToken);

        // Render gallery pages
        foreach (var gallery in model.Galleries)
        {
            var galleryImages = model.Images
                .Where(img => img.SourcePath.Contains(gallery.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var galleryHtml = templateEngine.Render(
                GetDefaultGalleryTemplate(),
                new { model.Site, gallery, images = galleryImages });

            var galleryOutputPath = Path.Combine(outputDirectory, gallery.Path);
            Directory.CreateDirectory(galleryOutputPath);

            await File.WriteAllTextAsync(
                Path.Combine(galleryOutputPath, "index.html"),
                galleryHtml,
                cancellationToken);
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
                                <a href="{{ url_for gallery.path }}" class="btn btn-primary">View Gallery</a>
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
                            <source srcset="{{ image_url image.file_name 1920 'webp' }}" type="image/webp">
                            <img src="{{ image_url image.file_name 1920 'jpg' }}" class="img-fluid" alt="{{ image.file_name }}">
                        </picture>

                        {{ if image.exif }}
                        <div class="small text-muted mt-2">
                            {{ image.exif.make }} {{ image.exif.model }}<br>
                            {{ format_exif_aperture image.exif.f_number }} ·
                            {{ format_exif_exposure image.exif.exposure_time }} ·
                            ISO {{ image.exif.iso }}
                        </div>
                        {{ end }}
                    </div>
                {{ end }}
                </div>

                <a href="/" class="btn btn-secondary mt-4">Back to Home</a>
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
    public DateTime BuildDate { get; init; }
}
