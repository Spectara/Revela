using Microsoft.Extensions.Options;
using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Commands.Generate.Scanning;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Models.Manifest;
using IManifestRepository = Spectara.Revela.Sdk.Abstractions.IManifestRepository;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for content scanning and building the unified site tree.
/// </summary>
/// <remarks>
/// <para>
/// Scans the source directory to discover galleries, images, and navigation structure.
/// Builds a unified root node containing the entire site hierarchy.
/// </para>
/// <para>
/// During scan, reads image metadata (dimensions, EXIF) for each discovered image.
/// This allows calculating target sizes and provides complete manifest data upfront.
/// </para>
/// </remarks>
public sealed partial class ContentService(
    ContentScanner contentScanner,
    NavigationBuilder navigationBuilder,
    IManifestRepository manifestRepository,
    IImageProcessor imageProcessor,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<GenerateConfig> options,
    ILogger<ContentService> logger) : IContentService
{
    /// <summary>Gets full path to source directory</summary>
    private string SourcePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Source);

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => options.CurrentValue.Images;

    /// <inheritdoc />
    public async Task<ContentResult> ScanAsync(
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            // Validate source directory exists
            if (!Directory.Exists(SourcePath))
            {
                return new ContentResult
                {
                    Success = false,
                    ErrorMessage = $"Source directory not found: {SourcePath}"
                };
            }

            progress?.Report(new ContentProgress
            {
                Status = "Loading manifest...",
                GalleriesFound = 0,
                ImagesFound = 0
            });

            // Load existing manifest
            await manifestRepository.LoadAsync(cancellationToken);

            progress?.Report(new ContentProgress
            {
                Status = "Scanning content...",
                GalleriesFound = 0,
                ImagesFound = 0
            });

            // Scan content
            var content = await contentScanner.ScanAsync(SourcePath, cancellationToken);

            progress?.Report(new ContentProgress
            {
                Status = "Reading image metadata...",
                GalleriesFound = content.Galleries.Count,
                ImagesFound = content.Images.Count
            });

            // Read metadata for all images (dimensions, EXIF)
            var imageMetadata = await ReadAllImageMetadataAsync(
                content.Images,
                progress,
                cancellationToken);

            progress?.Report(new ContentProgress
            {
                Status = "Building navigation...",
                GalleriesFound = content.Galleries.Count,
                ImagesFound = content.Images.Count
            });

            // Build navigation tree
            var navigation = await navigationBuilder.BuildAsync(SourcePath, cancellationToken: cancellationToken);

            // Build unified root node with metadata
            var root = BuildRoot(content, navigation, imageMetadata);

            // Update manifest
            manifestRepository.SetRoot(root);
            manifestRepository.LastScanned = DateTime.UtcNow;

            // Save manifest
            await manifestRepository.SaveAsync(cancellationToken);

            progress?.Report(new ContentProgress
            {
                Status = "Scan complete",
                GalleriesFound = content.Galleries.Count,
                ImagesFound = content.Images.Count
            });

            LogScanCompleted(logger, content.Galleries.Count, content.Images.Count);

            stopwatch.Stop();

            return new ContentResult
            {
                Success = true,
                GalleryCount = content.Galleries.Count,
                ImageCount = content.Images.Count,
                NavigationItemCount = CountManifestEntries(root),
                Duration = stopwatch.Elapsed
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            LogScanFailed(logger, ex);
            return new ContentResult
            {
                Success = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed
            };
        }
    }

    /// <summary>
    /// Read metadata for all discovered images.
    /// </summary>
    /// <remarks>
    /// Uses parallel NetVips access (header-only) for fast metadata extraction.
    /// Approximately 10-20ms per image vs 200-500ms for full processing.
    /// Parallelized using Environment.ProcessorCount for optimal throughput.
    /// Images below MinWidth or MinHeight thresholds are skipped.
    /// </remarks>
    private async Task<Dictionary<string, ImageMetadata>> ReadAllImageMetadataAsync(
        IReadOnlyList<SourceImage> images,
        IProgress<ContentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, ImageMetadata>(StringComparer.OrdinalIgnoreCase);
        var metadataLock = new object();
        var processedCount = 0;
        var skippedCount = 0;

        var minWidth = ImageSettings.MinWidth;
        var minHeight = ImageSettings.MinHeight;

        await Parallel.ForEachAsync(
            images,
            cancellationToken,
            async (image, ct) =>
            {
                try
                {
                    var meta = await imageProcessor.ReadMetadataAsync(image.SourcePath, ct);

                    // Skip images below minimum size thresholds
                    if ((minWidth > 0 && meta.Width < minWidth) ||
                        (minHeight > 0 && meta.Height < minHeight))
                    {
                        LogImageTooSmall(logger, image.SourcePath, meta.Width, meta.Height, minWidth, minHeight);
                        Interlocked.Increment(ref skippedCount);
                        Interlocked.Increment(ref processedCount);
                        return;
                    }

                    lock (metadataLock)
                    {
                        metadata[image.RelativePath] = meta;
                    }

                    var current = Interlocked.Increment(ref processedCount);

                    // Report progress every 10 images or on last image
                    if (current % 10 == 0 || current == images.Count)
                    {
                        progress?.Report(new ContentProgress
                        {
                            Status = $"Reading metadata... ({current}/{images.Count})",
                            GalleriesFound = 0,
                            ImagesFound = current
                        });
                    }
                }
                catch (Exception ex)
                {
                    // Log but don't fail - image may be corrupt or unsupported format
                    LogMetadataReadFailed(logger, image.SourcePath, ex);
                }
            });

        if (skippedCount > 0)
        {
            LogSmallImagesSkipped(logger, skippedCount, minWidth, minHeight);
        }

        return metadata;
    }

    #region Tree Building

    /// <summary>
    /// Build the unified root node from scanned content and navigation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The root node represents the home gallery with all navigation children.
    /// Gallery data (images, dates, etc.) is merged into navigation nodes.
    /// </para>
    /// <para>
    /// Images from content.Images are grouped by their Gallery path and
    /// assigned to the corresponding ManifestEntry nodes.
    /// </para>
    /// </remarks>
    private ManifestEntry BuildRoot(
        ContentTree content,
        IReadOnlyList<NavigationItem> navigation,
        Dictionary<string, ImageMetadata> imageMetadata)
    {
        // Find the home gallery (empty path)
        var homeGallery = content.Galleries.FirstOrDefault(g => string.IsNullOrEmpty(g.Path));

        // Build a lookup for galleries by slug for merging with navigation
        var galleryBySlug = new Dictionary<string, Gallery>(StringComparer.OrdinalIgnoreCase);
        foreach (var gallery in content.Galleries.Where(g => !string.IsNullOrEmpty(g.Slug)))
        {
            galleryBySlug[gallery.Slug] = gallery;
        }

        // Build a lookup for images by gallery path
        // Gallery path is the relative path to the directory containing the image
        var imagesByPath = content.Images
            .GroupBy(img => img.Gallery, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        // Build a lookup for markdown files by gallery path
        var markdownsByPath = content.Markdowns
            .GroupBy(md => md.Gallery, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        // Create root node from home gallery
        var rootImages = imagesByPath.GetValueOrDefault(string.Empty) ?? [];
        var rootMarkdowns = markdownsByPath.GetValueOrDefault(string.Empty) ?? [];
        var root = new ManifestEntry
        {
            Text = homeGallery?.Title ?? homeGallery?.Name ?? "Home",
            Slug = string.Empty,
            Path = string.Empty,
            Description = homeGallery?.Description,
            Cover = homeGallery?.Cover,
            Date = homeGallery?.Date,
            Featured = homeGallery?.Featured ?? false,
            Hidden = false,
            Template = null,  // Root uses default template
            DataSources = [],
            Content = BuildContentList(rootImages, rootMarkdowns, imageMetadata),
            Children = [.. navigation.Select(nav => ConvertNavigationToEntry(nav, galleryBySlug, imagesByPath, markdownsByPath, imageMetadata))]
        };

        return root;
    }

    /// <summary>
    /// Convert a NavigationItem to ManifestEntry, merging gallery data where available.
    /// </summary>
    private ManifestEntry ConvertNavigationToEntry(
        NavigationItem navItem,
        Dictionary<string, Gallery> galleryBySlug,
        Dictionary<string, List<SourceImage>> imagesByPath,
        Dictionary<string, List<SourceMarkdown>> markdownsByPath,
        Dictionary<string, ImageMetadata> imageMetadata)
    {
        // Try to find matching gallery by URL/slug
        Gallery? gallery = null;
        if (!string.IsNullOrEmpty(navItem.Url))
        {
            galleryBySlug.TryGetValue(navItem.Url, out gallery);
        }

        // Get images for this gallery path (only for gallery nodes, not branches)
        // Branch nodes (slug=null) don't have direct images - they only contain children
        List<SourceImage> images = [];
        List<SourceMarkdown> markdowns = [];
        if (gallery != null)
        {
            images = imagesByPath.GetValueOrDefault(gallery.Path) ?? [];
            markdowns = markdownsByPath.GetValueOrDefault(gallery.Path) ?? [];
        }

        return new ManifestEntry
        {
            Text = navItem.Text,
            Slug = navItem.Url,
            Path = gallery?.Path ?? BuildPathFromNavigation(navItem),
            Description = navItem.Description ?? gallery?.Description,
            Cover = gallery?.Cover,
            Date = gallery?.Date,
            Featured = gallery?.Featured ?? false,
            Hidden = navItem.Hidden,
            Template = gallery?.Template,
            DataSources = gallery?.DataSources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            Content = BuildContentList(images, markdowns, imageMetadata),
            Children = [.. navItem.Children.Select(child => ConvertNavigationToEntry(child, galleryBySlug, imagesByPath, markdownsByPath, imageMetadata))]
        };
    }

    /// <summary>
    /// Build a combined content list from images and markdown files, sorted by filename.
    /// </summary>
    /// <remarks>
    /// Content is sorted alphabetically by filename to allow users to control
    /// ordering via filename prefixes (e.g., "01-intro.md", "02-photo.jpg").
    /// </remarks>
    private List<GalleryContent> BuildContentList(
        List<SourceImage> images,
        List<SourceMarkdown> markdowns,
        Dictionary<string, ImageMetadata> imageMetadata)
    {
        var content = new List<GalleryContent>();

        // Convert images
        foreach (var image in images)
        {
            content.Add(ConvertSourceImage(image, imageMetadata));
        }

        // Convert markdown files
        foreach (var markdown in markdowns)
        {
            content.Add(ConvertSourceMarkdown(markdown));
        }

        // Sort by filename for predictable ordering
        return [.. content.OrderBy(c => c.Filename, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Build a path from navigation text for branch nodes without a gallery.
    /// </summary>
    private static string BuildPathFromNavigation(NavigationItem navItem) =>
        // Branch nodes don't have galleries, so we construct a path from the text
        // This is used for finding images later
        navItem.Text;

    /// <summary>
    /// Convert a SourceImage to ImageContent with metadata.
    /// </summary>
    /// <remarks>
    /// If metadata was successfully read, populates Width, Height, EXIF, DateTaken, and Sizes.
    /// Sizes are calculated based on the actual image width and configured size presets.
    /// </remarks>
    private ImageContent ConvertSourceImage(
        SourceImage source,
        Dictionary<string, ImageMetadata> imageMetadata)
    {
        // Try to get metadata for this image
        imageMetadata.TryGetValue(source.RelativePath, out var meta);

        // Calculate which sizes to generate (config sizes + original width)
        var sizes = meta != null
            ? CalculateSizes(meta.Width)
            : [];

        return new ImageContent
        {
            Filename = source.FileName,
            Hash = "", // Computed during image processing
            Width = meta?.Width ?? 0,
            Height = meta?.Height ?? 0,
            Sizes = sizes,
            FileSize = meta?.FileSize ?? source.FileSize,
            DateTaken = meta?.DateTaken,
            Exif = meta?.Exif,
            ProcessedAt = DateTime.MinValue
        };
    }

    /// <summary>
    /// Convert a SourceMarkdown to MarkdownContent.
    /// </summary>
    /// <remarks>
    /// The markdown body is NOT stored in the manifest - it's loaded at render time.
    /// Only metadata (filename, size, hash) is stored for change detection.
    /// </remarks>
    private static MarkdownContent ConvertSourceMarkdown(SourceMarkdown source)
    {
        return new MarkdownContent
        {
            Filename = source.FileName,
            FileSize = source.FileSize,
            Hash = ComputeMarkdownHash(source)
        };
    }

    /// <summary>
    /// Compute a hash for markdown change detection.
    /// </summary>
    private static string ComputeMarkdownHash(SourceMarkdown source)
    {
        var hashInput = $"{source.FileName}_{source.FileSize}_{source.LastModified.Ticks}";
        var hashBytes = System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(hashInput));
        return Convert.ToHexString(hashBytes)[..12];
    }

    /// <summary>
    /// Calculate which sizes to generate based on image width.
    /// </summary>
    /// <remarks>
    /// Includes configured sizes smaller than original width, plus the original width.
    /// Original width is included for full-resolution lightbox view.
    /// </remarks>
    private List<int> CalculateSizes(int imageWidth) =>
        // Filter configured sizes to only include those smaller than original
        // Then add original width for full-resolution lightbox
        [.. ImageSettings.Sizes.Where(s => s < imageWidth).Append(imageWidth).Order()];

    /// <summary>
    /// Counts total manifest entries including children recursively.
    /// </summary>
    private static int CountManifestEntries(ManifestEntry entry)
    {
        var count = 1; // Count self
        foreach (var child in entry.Children)
        {
            count += CountManifestEntries(child);
        }
        return count;
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan completed: {GalleryCount} galleries, {ImageCount} images")]
    private static partial void LogScanCompleted(ILogger logger, int galleryCount, int imageCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan failed")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read metadata for {ImagePath}")]
    private static partial void LogMetadataReadFailed(ILogger logger, string imagePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping small image {ImagePath} ({Width}x{Height}, min: {MinWidth}x{MinHeight})")]
    private static partial void LogImageTooSmall(ILogger logger, string imagePath, int width, int height, int minWidth, int minHeight);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped {SkippedCount} small images (below {MinWidth}x{MinHeight})")]
    private static partial void LogSmallImagesSkipped(ILogger logger, int skippedCount, int minWidth, int minHeight);

    #endregion
}
