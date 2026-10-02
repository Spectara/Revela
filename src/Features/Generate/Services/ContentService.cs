using System.Diagnostics;
using Microsoft.Extensions.Options;
using Spectara.Revela.Core.Abstractions;
using Spectara.Revela.Core.Services;
using Spectara.Revela.Features.Generate.Abstractions;
using Spectara.Revela.Features.Generate.Filtering;
using Spectara.Revela.Features.Generate.Infrastructure;
using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Features.Generate.Models.Results;
using Spectara.Revela.Sdk;
using Spectara.Revela.Sdk.Artifacts;
using Spectara.Revela.Sdk.Configuration;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;
using Spectara.Revela.Sdk.Services;

namespace Spectara.Revela.Features.Generate.Services;

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
internal sealed partial class ContentService(
    ContentScanner contentScanner,
    NavigationBuilder navigationBuilder,
    IManifestRepository manifestRepository,
    IImageProcessor imageProcessor,
    IImageSizesProvider imageSizesProvider,
    IPathResolver pathResolver,
    IThemeRegistry themeRegistry,
    IOptions<ProjectEnvironment> projectEnvironment,
    IOptionsMonitor<SiteCoreConfig> siteCoreConfig,
    IOptionsMonitor<ThemeConfig> themeConfig,
    IOptionsMonitor<GenerateConfig> generateOptions,
    IArtifactLifecycle artifactLifecycle,
    TimeProvider timeProvider,
    ILogger<ContentService> logger) : IContentService
{
    /// <summary>Gets full path to source directory (supports hot-reload)</summary>
    private string SourcePath => pathResolver.SourcePath;

    /// <summary>Gets current image settings (supports hot-reload)</summary>
    private ImageConfig ImageSettings => generateOptions.CurrentValue.Images;

    /// <summary>Gets current sorting settings (supports hot-reload)</summary>
    private SortingConfig SortingSettings => generateOptions.CurrentValue.Sorting;

    /// <inheritdoc />
    public async Task<ContentResult> ScanAsync(
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

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

            // Pre-check: the configured theme is not installed. Without it the scan
            // cannot resolve image sizes (imageSizesProvider.GetSizes() would throw).
            // Fail early with the same actionable message the pages step reports
            // instead of leaking a raw stack trace through the generic catch below.
            var themeName = string.IsNullOrEmpty(themeConfig.CurrentValue.Name) ? ThemeConfig.DefaultName : themeConfig.CurrentValue.Name;
            var theme = themeRegistry.Resolve(themeName, projectEnvironment.Value.Path);
            if (theme is null)
            {
                return new ContentResult
                {
                    Success = false,
                    ErrorMessage =
                        $"Theme '{themeName}' is not installed. " +
                        $"Run 'revela theme install {themeName}' (or pick an installed theme " +
                        "with 'revela config theme'). Run 'revela check' to diagnose your project."
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

            // Check if scan config changed - if so, don't use metadata cache
            var scanConfigHash = ManifestService.ComputeScanConfigHash(
                ImageSettings.Placeholder.Strategy,
                ImageSettings.MinWidth,
                ImageSettings.MinHeight);
            var scanConfigChanged = manifestRepository.ScanConfigHash != scanConfigHash;

            // Cache existing image entries for metadata caching (unless scan config changed)
            Dictionary<string, ImageContent> existingImages;
            if (scanConfigChanged && manifestRepository.Images.Count > 0)
            {
                LogScanConfigChanged(logger);
                existingImages = new Dictionary<string, ImageContent>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                existingImages = manifestRepository.Images
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => kvp.Value,
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

            // Reject empty or colliding normalized slugs before touching metadata, the
            // manifest, or any output — distinct sources must not overwrite each other (#97).
            if (content.SlugConflicts.Count > 0)
            {
                var slugError = SlugValidator.FormatScanError(content.SlugConflicts);
                LogSlugConflicts(logger, content.SlugConflicts.Count);
                stopwatch.Stop();
                return new ContentResult
                {
                    Success = false,
                    ErrorMessage = slugError,
                    Duration = stopwatch.Elapsed
                };
            }

            progress?.Report(new ContentProgress
            {
                Status = "Reading image metadata...",
                GalleriesFound = content.Galleries.Count,
                ImagesFound = content.Images.Count
            });

            // Read metadata for all images (dimensions, EXIF) - uses cache for unchanged images
            var imageMetadata = await ReadAllImageMetadataAsync(
                content.Images,
                existingImages,
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

            // Build unified root node with metadata
            var root = BuildRoot(content, navigation, imageMetadata);

            var invalidationResult = await artifactLifecycle.PrepareToReplaceAsync(
                CoreArtifacts.Manifest,
                cancellationToken);
            if (!invalidationResult.Success)
            {
                return new ContentResult
                {
                    Success = false,
                    ErrorMessage = invalidationResult.ErrorMessage,
                    Duration = stopwatch.Elapsed
                };
            }

            // Update manifest
            manifestRepository.SetRoot(root);
            manifestRepository.ScanConfigHash = scanConfigHash;
            manifestRepository.LastScanned = timeProvider.GetUtcNow().UtcDateTime;

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
    /// <para>
    /// Uses caching to skip unchanged images (based on FileSize + LastModified).
    /// Only new or modified images require NetVips metadata extraction.
    /// </para>
    /// <para>
    /// When cached metadata is available, it's reused directly without disk I/O.
    /// Approximately 10-20ms per image vs &lt;0.1ms for cached images.
    /// </para>
    /// <para>
    /// Images below MinWidth or MinHeight thresholds are skipped.
    /// </para>
    /// </remarks>
    private async Task<Dictionary<string, ImageMetadata>> ReadAllImageMetadataAsync(
        IReadOnlyList<SourceImage> images,
        Dictionary<string, ImageContent> existingImages,
        IProgress<ContentProgress>? progress,
        CancellationToken cancellationToken)
    {
        var metadata = new Dictionary<string, ImageMetadata>(StringComparer.OrdinalIgnoreCase);
        var metadataLock = new Lock();
        var processedCount = 0;
        var cachedCount = 0;
        var newCount = 0;
        var skippedCount = 0;

        var minWidth = ImageSettings.MinWidth;
        var minHeight = ImageSettings.MinHeight;

        // Get placeholder config - will be null if strategy is None (= no placeholder generation during scan)
        var placeholderConfig = ImageSettings.Placeholder.Strategy != PlaceholderStrategy.None
            ? ImageSettings.Placeholder
            : null;

        await Parallel.ForEachAsync(
            images,
            cancellationToken,
            async (image, ct) =>
            {
                try
                {
                    // Normalize path for cache lookup (forward slashes)
                    var normalizedPath = image.RelativePath.Replace('\\', '/');

                    // Check if we can use cached metadata (same file size and modification time)
                    ImageMetadata meta;
                    if (existingImages.TryGetValue(normalizedPath, out var cached) &&
                        cached.FileSize == image.FileSize &&
                        cached.LastModified == image.LastModified)
                    {
                        // Cache hit - reconstruct ImageMetadata from cached ImageContent
                        meta = new ImageMetadata
                        {
                            Width = cached.Width,
                            Height = cached.Height,
                            FileSize = cached.FileSize,
                            Exif = cached.Exif,
                            DateTaken = cached.DateTaken,
                            Title = cached.Title,
                            Description = cached.Description,
                            Keywords = cached.Keywords,
                            Rating = cached.Rating,
                            Placeholder = cached.Placeholder
                        };
                        Interlocked.Increment(ref cachedCount);
                    }
                    else
                    {
                        // Cache miss - read metadata from disk
                        meta = await imageProcessor.ReadMetadataAsync(image.SourcePath, placeholderConfig, ct);
                        Interlocked.Increment(ref newCount);
                    }

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
                            Status = $"Reading metadata... ({current}/{images.Count}) ({cachedCount} cached, {newCount} new)",
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

        LogMetadataCacheStats(logger, cachedCount, newCount, skippedCount);

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
        // Exclude shared images (_images) - they are only available via filters and content images
        var imagesByPath = content.Images
            .Where(img => !img.Gallery.Equals(ProjectPaths.SharedImages, StringComparison.OrdinalIgnoreCase))
            .GroupBy(img => img.Gallery, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                g => g.Key,
                g => g.ToList(),
                StringComparer.OrdinalIgnoreCase);

        // Pre-convert ALL images to ImageContent for filter galleries
        // This allows filters to query across the entire site
        var allImages = ConvertAllImagesToContent(content.Images, imageMetadata);

        // Create context for building entries (includes all images for filtering)
        var buildContext = new EntryBuildContext(
            galleryBySlug,
            imagesByPath,
            imageMetadata,
            allImages);

        // Create root node from home gallery
        var rootImages = imagesByPath.GetValueOrDefault(string.Empty) ?? [];

        // Build children: navigation entries + optional shared images node
        var children = navigation.Select(nav => ConvertNavigationToEntry(nav, buildContext)).ToList();

        // Add hidden _images node for shared images so they appear in the manifest tree.
        // This ensures shared images are processed by the image pipeline and available
        // via manifestRepository.Images for content image resolution.
        var sharedImages = content.Images
            .Where(img => img.Gallery.Equals(ProjectPaths.SharedImages, StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (sharedImages.Count > 0)
        {
            var sharedContent = sharedImages
                .Where(img => imageMetadata.ContainsKey(img.RelativePath))
                .Select(img => CreateImageContent(img, imageMetadata[img.RelativePath]))
                .ToList();

            if (sharedContent.Count > 0)
            {
                children.Add(new ManifestEntry
                {
                    Text = ProjectPaths.SharedImages,
                    Slug = null,
                    Path = ProjectPaths.SharedImages,
                    Hidden = true,
                    Content = sharedContent,
                    Children = []
                });
            }
        }

        var root = new ManifestEntry
        {
            Text = RootTitle(homeGallery),
            Slug = RelativePath.Empty,
            Path = RelativePath.Empty,
            Description = homeGallery?.Description,
            Cover = RelativePath.FromNullable(homeGallery?.Cover),
            Hidden = false,
            Template = homeGallery?.Template,
            DataSources = homeGallery?.DataSources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            Content = BuildContentList(rootImages, homeGallery?.Sort, homeGallery?.Filter, buildContext),
            Children = children
        };

        return root;
    }

    /// <summary>
    /// Title of the home page: its front matter title, else the site title, else the project
    /// folder name — never a hard-coded word in a language the site may not use.
    /// </summary>
    private string RootTitle(Gallery? homeGallery)
    {
        if (!string.IsNullOrWhiteSpace(homeGallery?.Title))
        {
            return homeGallery.Title;
        }

        var siteTitle = siteCoreConfig.CurrentValue.Title;
        return !string.IsNullOrWhiteSpace(siteTitle)
            ? siteTitle
            : Path.GetFileName(Path.TrimEndingDirectorySeparator(projectEnvironment.Value.Path));
    }

    /// <summary>
    /// Context for building manifest entries, including pre-converted images for filtering.
    /// </summary>
    private sealed record EntryBuildContext(
        Dictionary<string, Gallery> GalleryBySlug,
        Dictionary<string, List<SourceImage>> ImagesByPath,
        Dictionary<string, ImageMetadata> ImageMetadata,
        IReadOnlyList<ImageContent> AllImages);

    /// <summary>
    /// Convert all source images to ImageContent for use in filter expressions.
    /// </summary>
    private List<ImageContent> ConvertAllImagesToContent(
        IReadOnlyList<SourceImage> images,
        Dictionary<string, ImageMetadata> imageMetadata)
    {
        var result = new List<ImageContent>(images.Count);

        foreach (var image in images)
        {
            if (imageMetadata.TryGetValue(image.RelativePath, out var meta))
            {
                result.Add(CreateImageContent(image, meta));
            }
        }

        return result;
    }

    /// <summary>
    /// Create an ImageContent from a SourceImage and its metadata.
    /// </summary>
    private ImageContent CreateImageContent(
        SourceImage image,
        ImageMetadata meta)
    {
        // Calculate sizes based on actual image dimensions
        var sizes = CalculateSizes(meta.Width);

        return new ImageContent
        {
            Filename = image.FileName,
            SourcePath = image.RelativePath.Replace('\\', '/'),
            FileSize = image.FileSize,
            LastModified = image.LastModified,
            Width = meta.Width,
            Height = meta.Height,
            Sizes = sizes,
            DateTaken = meta.DateTaken,
            Exif = meta.Exif,
            Title = meta.Title,
            Description = meta.Description,
            Keywords = meta.Keywords,
            Rating = meta.Rating,
            Placeholder = meta.Placeholder
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

        // Branch nodes (slug=null) don't have direct images - they only contain children
        var images = gallery is null
            ? []
            : context.ImagesByPath.GetValueOrDefault(gallery.Path) ?? [];

        return new ManifestEntry
        {
            Text = navItem.Text,
            Slug = RelativePath.FromNullable(navItem.Url),
            Path = gallery?.Path ?? BuildPathFromNavigation(navItem),
            Description = navItem.Description ?? gallery?.Description,
            Cover = RelativePath.FromNullable(gallery?.Cover),
            Hidden = navItem.Hidden,
            Pinned = navItem.Pinned,
            Template = gallery?.Template,
            DataSources = gallery?.DataSources.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) ?? [],
            Content = BuildContentList(images, gallery?.Sort, gallery?.Filter, context),
            Children = [.. navItem.Children.Select(child => ConvertNavigationToEntry(child, context))]
        };
    }

    /// <summary>
    /// Build a page's image list: the images matching its filter, else its folder's images.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A filter selects from ALL site images instead of the page's folder; an invalid filter
    /// throws a <see cref="FilterParseException"/> with the position of the error.
    /// </para>
    /// <para>
    /// Folder images use the same sort as filters (see <see cref="FilterService.Sort"/>):
    /// the configured image sort, overridden by the page's <c>sort</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="FilterParseException">Thrown when the filter expression is invalid.</exception>
    private List<ImageContent> BuildContentList(
        List<SourceImage> folderImages,
        string? sortOverride,
        string? filterExpression,
        EntryBuildContext context)
    {
        if (!string.IsNullOrEmpty(filterExpression))
        {
            return [.. FilterService.ApplyQuery(context.AllImages, filterExpression, sortOverride, SortingSettings.Images)];
        }

        var images = folderImages.Select(image => ConvertSourceImage(image, context.ImageMetadata));
        return [.. FilterService.Sort(images, sortOverride, SortingSettings.Images)];
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
            SourcePath = source.RelativePath.Replace('\\', '/'),
            Width = meta?.Width ?? 0,
            Height = meta?.Height ?? 0,
            Sizes = sizes,
            FileSize = meta?.FileSize ?? source.FileSize,
            LastModified = source.LastModified,
            DateTaken = meta?.DateTaken,
            Exif = meta?.Exif,
            Title = meta?.Title,
            Description = meta?.Description,
            Keywords = meta?.Keywords ?? [],
            Rating = meta?.Rating,
            Placeholder = meta?.Placeholder
        };
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

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan rejected: {ConflictCount} slug conflict(s) would overwrite generated output")]
    private static partial void LogSlugConflicts(ILogger logger, int conflictCount);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to read metadata for {ImagePath}")]
    private static partial void LogMetadataReadFailed(ILogger logger, string imagePath, Exception exception);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping small image {ImagePath} ({Width}x{Height}, min: {MinWidth}x{MinHeight})")]
    private static partial void LogImageTooSmall(ILogger logger, string imagePath, int width, int height, int minWidth, int minHeight);

    [LoggerMessage(Level = LogLevel.Information, Message = "Skipped {SkippedCount} small images (below {MinWidth}x{MinHeight})")]
    private static partial void LogSmallImagesSkipped(ILogger logger, int skippedCount, int minWidth, int minHeight);

    [LoggerMessage(Level = LogLevel.Information, Message = "Metadata cache: {CachedCount} cached, {NewCount} new, {SkippedCount} skipped")]
    private static partial void LogMetadataCacheStats(ILogger logger, int cachedCount, int newCount, int skippedCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan config changed, all metadata will be re-read")]
    private static partial void LogScanConfigChanged(ILogger logger);

    #endregion
}
