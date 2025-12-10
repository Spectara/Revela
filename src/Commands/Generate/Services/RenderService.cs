using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;

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
    IManifestRepository manifestRepository,
    IConfiguration configuration,
    IOptions<RevelaConfig> options,
    ILogger<RenderService> logger) : IRenderService
{
    /// <summary>Output directory for generated site</summary>
    private const string OutputDirectory = "output";

    /// <summary>Image settings from configuration</summary>
    private readonly ImageSettings imageSettings = options.Value.Generate.Images;

    /// <inheritdoc />
    public void SetTheme(IThemePlugin? theme) => templateEngine.SetTheme(theme);

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

            // Resolve theme
            var theme = themeResolver.Resolve(config.Theme.Name, Environment.CurrentDirectory);
            SetTheme(theme);

            // Reconstruct galleries and navigation from unified root
            var galleries = ReconstructGalleries(manifestRepository.Root);
            var navigation = ReconstructNavigation(manifestRepository.Root);

            progress?.Report(new RenderProgress
            {
                CurrentPage = "Preparing...",
                Rendered = 0,
                Total = galleries.Count + 1 // +1 for index
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

            // Copy theme assets
            if (theme is not null)
            {
                await CopyThemeAssetsAsync(theme, cancellationToken);
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

    private RevelaConfig LoadConfiguration()
    {
        var generateSettings = new GenerateSettings();
        configuration.GetSection("generate").Bind(generateSettings);

        return new RevelaConfig
        {
            Project = new ProjectSettings
            {
                Name = configuration["name"] ?? "Revela Site",
                BaseUrl = configuration["url"] ?? "https://example.com",
                Language = configuration["language"] ?? "en",
                ImageBasePath = configuration["imageBasePath"],
                BasePath = NormalizeBasePath(configuration["basePath"])
            },
            Site = new SiteSettings
            {
                Title = configuration["title"],
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
            var sourcePath = string.IsNullOrEmpty(entry.Path)
                ? imageEntry.Filename
                : $"{entry.Path}\\{imageEntry.Filename}";
            images.Add(Image.FromManifestEntry(sourcePath, imageEntry));
        }

        return new Gallery
        {
            Path = entry.Path,
            Slug = entry.Slug ?? string.Empty,
            Name = entry.Text,
            Title = entry.Text,
            Description = entry.Description,
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
        RevelaConfig config,
        IThemePlugin? theme,
        IProgress<RenderProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(OutputDirectory);

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

        var pageCount = 0;
        var totalPages = model.Galleries.Count + 1;

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
        var themeVariables = manifest?.Variables ?? new Dictionary<string, string>();
        var indexHtml = templateEngine.Render(
            indexTemplate,
            new
            {
                site = model.Site,
                gallery = new { title = "Home", body = (string?)null },
                galleries = model.Galleries,
                images = model.Images,
                nav_items = indexNavigation,
                basepath = indexBasePath,
                image_basepath = indexImageBasePath,
                image_formats = imageSettings.Formats.Keys,
                theme = themeVariables
            });

        await File.WriteAllTextAsync(
            Path.Combine(OutputDirectory, "index.html"),
            indexHtml,
            cancellationToken);
        pageCount++;

        // Render gallery pages
        foreach (var gallery in model.Galleries)
        {
            progress?.Report(new RenderProgress
            {
                CurrentPage = $"{gallery.Slug}/index.html",
                Rendered = pageCount,
                Total = totalPages
            });

            var galleryImages = model.Images
                .Where(img => img.SourcePath.Contains(gallery.Path, StringComparison.OrdinalIgnoreCase))
                .ToList();

            var relativeBasePath = UrlBuilder.CalculateBasePath(gallery.Slug);
            var basepath = CalculateSiteBasePath(config, relativeBasePath);
            var galleryNavigation = SetActiveState(model.Navigation, gallery.Slug);
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
                    image_basepath = galleryImageBasePath,
                    image_formats = imageSettings.Formats.Keys,
                    theme = themeVariables
                });

            var galleryOutputPath = Path.Combine(OutputDirectory, gallery.Slug);
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
            Children = activeChildren
        };
    }

    private static string CalculateSiteBasePath(RevelaConfig config, string relativeBasePath)
    {
        if (config.Project.BasePath == "/")
        {
            return relativeBasePath;
        }
        return config.Project.BasePath;
    }

    private static string CalculateImageBasePath(RevelaConfig config, string basepath)
    {
        if (!string.IsNullOrEmpty(config.Project.ImageBasePath))
        {
            return config.Project.ImageBasePath;
        }
        return $"{basepath}images/";
    }

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

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Generated {Count} pages")]
    private static partial void LogPagesGenerated(ILogger logger, int count);

    [LoggerMessage(Level = LogLevel.Error, Message = "Page generation failed")]
    private static partial void LogPagesGenerationFailed(ILogger logger, Exception exception);

    #endregion
}
