using Microsoft.Extensions.Options;

using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Filtering;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Commands.Generate.Scanning;
using Spectara.Revela.Core.Configuration;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Models;
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
    IImageSizesProvider imageSizesProvider,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<GenerateConfig> generateOptions,
    ILogger<ContentService> logger) : IContentService
{
    /// <summary>Gets full path to source directory</summary>
    private string SourcePath => Path.Combine(projectEnvironment.Value.Path, ProjectPaths.Source);

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => generateOptions.CurrentValue.Images;

    /// <summary>Gets current sorting settings (supports hot-reload)</summary>
    private SortingConfig SortingSettings => generateOptions.CurrentValue.Sorting;

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

            // Load existing manifest (to preserve image hashes for incremental builds)
            await manifestRepository.LoadAsync(cancellationToken);

            // Check if image config changed - if so, don't preserve old hashes
            var sizes = imageSizesProvider.GetSizes();
            var formats = generateOptions.CurrentValue.Images.GetActiveFormats();
            var configHash = ManifestService.ComputeConfigHash(sizes, formats);
            var configChanged = manifestRepository.ConfigHash != configHash;

            // Cache existing image hashes before rebuilding tree (unless config changed)
            Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes;
            if (configChanged && manifestRepository.Images.Count > 0)
            {
                LogConfigChanged(logger);
                existingHashes = new Dictionary<string, (string, DateTime)>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                existingHashes = manifestRepository.Images
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => (kvp.Value.Hash, kvp.Value.ProcessedAt),
                        StringComparer.OrdinalIgnoreCase);
            }

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
            var sortDescending = SortingSettings.Galleries == SortDirection.Desc;
            var navigation = await navigationBuilder.BuildAsync(
                SourcePath,
                sortDescending: sortDescending,
                cancellationToken: cancellationToken);

            // Build unified root node with metadata (preserving existing hashes)
            var root = BuildRoot(content, navigation, imageMetadata, existingHashes);

            // Update manifest
            manifestRepository.SetRoot(root);
            manifestRepository.ConfigHash = configHash;
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
    /// <para>
    /// Galleries with a <c>filter</c> property receive images matching the filter
    /// from the entire site, instead of only images in their directory.
    /// </para>
    /// <para>
    /// Existing image hashes are preserved for incremental builds.
    /// Images that no longer exist in source are automatically excluded.
    /// </para>
    /// </remarks>
    private ManifestEntry BuildRoot(
        ContentTree content,
        IReadOnlyList<NavigationItem> navigation,
        Dictionary<string, ImageMetadata> imageMetadata,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes)
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
        // Exclude shared images (_images) - they are only available via filters
        var imagesByPath = content.Images
            .Where(img => !img.Gallery.Equals(ProjectPaths.SharedImages, StringComparison.OrdinalIgnoreCase))
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

        // Pre-convert ALL images to ImageContent for filter galleries
        // This allows filters to query across the entire site
        var allImages = ConvertAllImagesToContent(content.Images, imageMetadata, existingHashes);

        // Create context for building entries (includes all images for filtering)
        var buildContext = new EntryBuildContext(
            galleryBySlug,
            imagesByPath,
            markdownsByPath,
            imageMetadata,
            existingHashes,
            allImages);

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
            Filter = homeGallery?.Filter,
            DataSources = [],
            Content = BuildContentListWithFilter(rootImages, rootMarkdowns, homeGallery?.Sort, homeGallery?.Filter, buildContext),
            Children = [.. navigation.Select(nav => ConvertNavigationToEntry(nav, buildContext))]
        };

        return root;
    }

    /// <summary>
    /// Context for building manifest entries, including pre-converted images for filtering.
    /// </summary>
    private sealed record EntryBuildContext(
        Dictionary<string, Gallery> GalleryBySlug,
        Dictionary<string, List<SourceImage>> ImagesByPath,
        Dictionary<string, List<SourceMarkdown>> MarkdownsByPath,
        Dictionary<string, ImageMetadata> ImageMetadata,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> ExistingHashes,
        IReadOnlyList<ImageContent> AllImages);

    /// <summary>
    /// Convert all source images to ImageContent for use in filter expressions.
    /// </summary>
    private List<ImageContent> ConvertAllImagesToContent(
        IReadOnlyList<SourceImage> images,
        Dictionary<string, ImageMetadata> imageMetadata,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes)
    {
        var result = new List<ImageContent>(images.Count);

        foreach (var image in images)
        {
            if (imageMetadata.TryGetValue(image.RelativePath, out var meta))
            {
                result.Add(CreateImageContent(image, meta, existingHashes));
            }
        }

        return result;
    }

    /// <summary>
    /// Create an ImageContent from a SourceImage and its metadata.
    /// </summary>
    private ImageContent CreateImageContent(
        SourceImage image,
        ImageMetadata meta,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes)
    {
        // Calculate sizes based on actual image dimensions
        var sizes = CalculateSizes(meta.Width);

        // Preserve existing hash if available
        string hash;
        DateTime processedAt;
        if (existingHashes.TryGetValue(image.RelativePath, out var existing))
        {
            hash = existing.Hash;
            processedAt = existing.ProcessedAt;
        }
        else
        {
            hash = string.Empty;
            processedAt = default;
        }

        return new ImageContent
        {
            Filename = image.FileName,
            SourcePath = image.RelativePath.Replace('\\', '/'),
            FileSize = image.FileSize,
            Hash = hash,
            Width = meta.Width,
            Height = meta.Height,
            Sizes = sizes,
            DateTaken = meta.DateTaken,
            Exif = meta.Exif,
            ProcessedAt = processedAt
        };
    }

    /// <summary>
    /// Convert a NavigationItem to ManifestEntry, merging gallery data where available.
    /// </summary>
    private ManifestEntry ConvertNavigationToEntry(
        NavigationItem navItem,
        EntryBuildContext context)
    {
        // Try to find matching gallery by URL/slug
        Gallery? gallery = null;
        if (!string.IsNullOrEmpty(navItem.Url))
        {
            context.GalleryBySlug.TryGetValue(navItem.Url, out gallery);
        }

        // Get images for this gallery path (only for gallery nodes, not branches)
        // Branch nodes (slug=null) don't have direct images - they only contain children
        List<SourceImage> images = [];
        List<SourceMarkdown> markdowns = [];
        if (gallery != null)
        {
            images = context.ImagesByPath.GetValueOrDefault(gallery.Path) ?? [];
            markdowns = context.MarkdownsByPath.GetValueOrDefault(gallery.Path) ?? [];
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
            Filter = gallery?.Filter,
            DataSources = gallery?.DataSources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            Content = BuildContentListWithFilter(images, markdowns, gallery?.Sort, gallery?.Filter, context),
            Children = [.. navItem.Children.Select(child => ConvertNavigationToEntry(child, context))]
        };
    }

    /// <summary>
    /// Build a combined content list, optionally using a filter expression.
    /// </summary>
    /// <remarks>
    /// <para>
    /// When a filter expression is provided, images are selected from ALL site images
    /// that match the filter, instead of only images in the gallery's directory.
    /// </para>
    /// <para>
    /// If the filter expression is invalid, a <see cref="FilterParseException"/> is thrown
    /// with a detailed error message including the position of the error.
    /// </para>
    /// </remarks>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    private List<GalleryContent> BuildContentListWithFilter(
        List<SourceImage> folderImages,
        List<SourceMarkdown> markdowns,
        string? sortOverride,
        string? filterExpression,
        EntryBuildContext context)
    {
        List<GalleryContent> content;

        if (!string.IsNullOrEmpty(filterExpression))
        {
            // Filter mode: select images from ALL site images matching the filter
            // ApplyQuery handles filter, sort (via pipe), and limit in one pass
            var filteredImages = FilterService.ApplyQuery(context.AllImages, filterExpression);
            content = [.. filteredImages];

            // Add markdown files from the folder (filters only apply to images)
            foreach (var markdown in markdowns)
            {
                content.Add(ConvertSourceMarkdown(markdown));
            }

            // Check if filter has its own sort clause - if so, skip gallery sort
            var query = FilterService.ParseQuery(filterExpression);
            if (query.HasSort)
            {
                return content; // Already sorted by filter's sort clause
            }
        }
        else
        {
            // Normal mode: use images from the folder
            content = BuildContentList(folderImages, markdowns, context.ImageMetadata, context.ExistingHashes, sortOverride);
            return content; // Already sorted by BuildContentList
        }

        // Sort filtered content using gallery/global sort
        return SortContent(content, sortOverride);
    }

    /// <summary>
    /// Build a combined content list from images and markdown files.
    /// </summary>
    /// <remarks>
    /// Content is sorted based on the configured image sort order.
    /// Default: alphabetically by filename to allow users to control
    /// ordering via filename prefixes (e.g., "01-intro.md", "02-photo.jpg").
    /// Alternative: by EXIF DateTaken for chronological galleries.
    /// Existing image hashes are preserved for incremental builds.
    /// </remarks>
    private List<GalleryContent> BuildContentList(
        List<SourceImage> images,
        List<SourceMarkdown> markdowns,
        Dictionary<string, ImageMetadata> imageMetadata,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes,
        string? sortOverride)
    {
        var content = new List<GalleryContent>();

        // Convert images (preserving existing hashes)
        foreach (var image in images)
        {
            content.Add(ConvertSourceImage(image, imageMetadata, existingHashes));
        }

        // Convert markdown files
        foreach (var markdown in markdowns)
        {
            content.Add(ConvertSourceMarkdown(markdown));
        }

        // Sort based on configuration (with optional gallery override)
        return SortContent(content, sortOverride);
    }

    /// <summary>
    /// Sort content based on the configured image sort settings.
    /// </summary>
    /// <remarks>
    /// Uses configurable field path with fallback for null values.
    /// Gallery sort override format: "field" or "field:direction".
    /// </remarks>
    private List<GalleryContent> SortContent(List<GalleryContent> content, string? sortOverride)
    {
        var config = SortingSettings.Images;
        var field = config.Field;
        var direction = config.Direction;
        var fallback = config.Fallback;

        // Parse gallery sort override: "field" or "field:direction"
        if (!string.IsNullOrEmpty(sortOverride))
        {
            var parts = sortOverride.Split(':', 2);
            field = parts[0];

            if (parts.Length > 1)
            {
                direction = parts[1].ToUpperInvariant() switch
                {
                    "ASC" => SortDirection.Asc,
                    "DESC" => SortDirection.Desc,
                    _ => direction // Keep global default if invalid
                };
            }
        }

        var sorted = direction == SortDirection.Asc
            ? content.OrderBy(c => GetSortKey(c, field, fallback), SortKeyComparer.Instance)
            : content.OrderByDescending(c => GetSortKey(c, field, fallback), SortKeyComparer.Instance);

        // Always use filename as final tie-breaker for stable sorting
        return [.. sorted.ThenBy(c => c.Filename, StringComparer.OrdinalIgnoreCase)];
    }

    /// <summary>
    /// Get a comparable sort key from content using the specified field path.
    /// </summary>
    /// <param name="content">The content item to extract the sort key from.</param>
    /// <param name="field">Primary field path (e.g., "dateTaken", "exif.focalLength").</param>
    /// <param name="fallback">Fallback field when primary is null.</param>
    /// <returns>A comparable object for sorting (string, DateTime, or number).</returns>
    private static object GetSortKey(GalleryContent content, string field, string fallback)
    {
        var value = GetFieldValue(content, field);
        if (value is null or "")
        {
            value = GetFieldValue(content, fallback);
        }

        return value ?? string.Empty;
    }

    /// <summary>
    /// Extract a field value from content using dot notation path.
    /// </summary>
    /// <remarks>
    /// Supported paths:
    /// <list type="bullet">
    ///   <item><c>filename</c> - GalleryContent.Filename</item>
    ///   <item><c>dateTaken</c> - ImageContent.DateTaken</item>
    ///   <item><c>exif.focalLength</c> - Typed EXIF property</item>
    ///   <item><c>exif.raw.Rating</c> - Raw EXIF dictionary value</item>
    /// </list>
    /// </remarks>
    private static object? GetFieldValue(GalleryContent content, string fieldPath)
    {
        var parts = fieldPath.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0)
        {
            return null;
        }

        var root = parts[0].ToUpperInvariant();

        return root switch
        {
            "FILENAME" => content.Filename,
            "DATETAKEN" when content is ImageContent img => img.DateTaken ?? DateTime.MaxValue,
            "EXIF" when content is ImageContent img && img.Exif is not null => GetExifFieldValue(img.Exif, parts.AsSpan()[1..]),
            _ => null
        };
    }

    /// <summary>
    /// Extract a field value from EXIF data using property path.
    /// </summary>
    private static object? GetExifFieldValue(ExifData exif, ReadOnlySpan<string> path)
    {
        if (path.IsEmpty)
        {
            return null;
        }

        var field = path[0].ToUpperInvariant();

        // Check for raw dictionary access: exif.raw.{FieldName}
        if (field == "RAW" && path.Length > 1 && exif.Raw is not null)
        {
            var rawField = path[1]; // Keep original case for dictionary lookup
            return exif.Raw.TryGetValue(rawField, out var rawValue) ? rawValue : null;
        }

        // Typed EXIF properties (match ExifData property names)
        return field switch
        {
            "MAKE" => exif.Make,
            "MODEL" => exif.Model,
            "LENSMODEL" => exif.LensModel,
            "FOCALLENGTH" => exif.FocalLength,
            "FNUMBER" => exif.FNumber,
            "EXPOSURETIME" => exif.ExposureTime,
            "ISO" => exif.Iso,
            "DATETAKEN" => exif.DateTaken,
            "GPSLATITUDE" => exif.GpsLatitude,
            "GPSLONGITUDE" => exif.GpsLongitude,
            _ => null
        };
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
    /// Existing hash and ProcessedAt are preserved for incremental builds.
    /// </remarks>
    private ImageContent ConvertSourceImage(
        SourceImage source,
        Dictionary<string, ImageMetadata> imageMetadata,
        Dictionary<string, (string Hash, DateTime ProcessedAt)> existingHashes)
    {
        // Try to get metadata for this image
        imageMetadata.TryGetValue(source.RelativePath, out var meta);

        // Calculate which sizes to generate (config sizes + original width)
        var sizes = meta != null
            ? CalculateSizes(meta.Width)
            : [];

        // Preserve existing hash if available (for incremental builds)
        // Use forward slashes for consistent key lookup
        var manifestKey = source.RelativePath.Replace('\\', '/');
        var (existingHash, existingProcessedAt) = existingHashes.TryGetValue(manifestKey, out var cached)
            ? cached
            : (string.Empty, DateTime.MinValue);

        return new ImageContent
        {
            Filename = source.FileName,
            SourcePath = source.RelativePath.Replace('\\', '/'),
            Hash = existingHash,
            Width = meta?.Width ?? 0,
            Height = meta?.Height ?? 0,
            Sizes = sizes,
            FileSize = meta?.FileSize ?? source.FileSize,
            DateTaken = meta?.DateTaken,
            Exif = meta?.Exif,
            ProcessedAt = existingProcessedAt
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
            SourcePath = source.RelativePath.Replace('\\', '/'),
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
    /// Sizes come from theme configuration (theme defines responsive breakpoints).
    /// </remarks>
    private List<int> CalculateSizes(int imageWidth)
    {
        // Get sizes from theme via provider (handles local override vs theme default)
        var themeSizes = imageSizesProvider.GetSizes();

        // Filter configured sizes to only include those smaller than original
        // Then add original width for full-resolution lightbox
        return [.. themeSizes.Where(s => s < imageWidth).Append(imageWidth).Order()];
    }

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

    [LoggerMessage(Level = LogLevel.Information, Message = "Image config changed, all images will be reprocessed")]
    private static partial void LogConfigChanged(ILogger logger);

    #endregion

    #region Nested Types

    /// <summary>
    /// Comparer for heterogeneous sort keys (handles DateTime, string, numbers).
    /// </summary>
    private sealed class SortKeyComparer : IComparer<object>
    {
        public static readonly SortKeyComparer Instance = new();

        public int Compare(object? x, object? y)
        {
            // Handle nulls
            if (x is null && y is null)
            {
                return 0;
            }

            if (x is null)
            {
                return -1;
            }

            if (y is null)
            {
                return 1;
            }

            // Same type: use natural comparison
            if (x.GetType() == y.GetType() && x is IComparable comparableX)
            {
                return comparableX.CompareTo(y);
            }

            // Different types: convert to string for comparison
            var strX = x switch
            {
                DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => x.ToString() ?? string.Empty
            };

            var strY = y switch
            {
                DateTime dt => dt.ToString("O", System.Globalization.CultureInfo.InvariantCulture),
                IFormattable f => f.ToString(null, System.Globalization.CultureInfo.InvariantCulture),
                _ => y.ToString() ?? string.Empty
            };

            return string.Compare(strX, strY, StringComparison.OrdinalIgnoreCase);
        }
    }

    #endregion
}
