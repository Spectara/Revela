using Spectara.Revela.Features.Generate.Models;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Features.Generate.Infrastructure;

/// <summary>
/// Scans content directory for images and content files
/// </summary>
/// <remarks>
/// Discovers:
/// - Image files (*.jpg, *.jpeg, *.png, *.webp, *.gif)
/// - Content files (_index.revela for gallery metadata)
/// - Directory structure (galleries/albums)
///
/// Creates a content tree representing the site structure.
/// </remarks>
internal sealed partial class ContentScanner(
    ILogger<ContentScanner> logger,
    RevelaParser revelaParser)
{

    /// <summary>
    /// Scans directory and returns content tree
    /// </summary>
    public async Task<ContentTree> ScanAsync(string sourceDirectory, CancellationToken cancellationToken = default)
    {
        LogScanningDirectory(logger, sourceDirectory);

        var images = new List<SourceImage>();
        var galleries = new List<Gallery>();

        // Scan root directory
        await ScanDirectoryAsync(sourceDirectory, string.Empty, images, galleries, cancellationToken);

        // Detect empty or colliding normalized output slugs across everything we enumerated.
        // The scan step fails on these before any rendering — see ContentService.ScanAsync.
        var slugConflicts = SlugValidator.FindConflicts(galleries, images);

        LogScanComplete(logger, images.Count, galleries.Count);

        return new ContentTree
        {
            Images = images,
            Galleries = galleries,
            SlugConflicts = slugConflicts
        };
    }

    private async Task ScanDirectoryAsync(
        string baseDirectory,
        string relativePath,
        List<SourceImage> images,
        List<Gallery> galleries,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var currentDirectory = string.IsNullOrEmpty(relativePath)
            ? baseDirectory
            : Path.Combine(baseDirectory, relativePath);

        if (!Directory.Exists(currentDirectory))
        {
            return;
        }

        // Find images in current directory
        var imageFiles = Directory.EnumerateFiles(currentDirectory)
            .Where(f => SupportedImageExtensions.IsSupported(Path.GetExtension(f)))
            .ToList();

        // Check for _index.revela (gallery metadata or standalone page)
        var hasIndexFile = File.Exists(Path.Combine(currentDirectory, RevelaParser.IndexFileName));

        // Create gallery if directory has images, has _index.revela (text/filter pages), or is root with metadata
        // Note: Root (_index.revela at source root) is always created if it exists - allows filter galleries on homepage
        var isRoot = string.IsNullOrEmpty(relativePath);
        var shouldCreateGallery = imageFiles.Count > 0 || hasIndexFile;

        if (shouldCreateGallery)
        {
            // This directory contains images or is a text-only page
            var directoryMetadata = await LoadGalleryMetadataAsync(currentDirectory, cancellationToken);

            // Build URL-safe slug from path segments
            var pathSegments = string.IsNullOrEmpty(relativePath)
                ? []
                : relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var slug = UrlBuilder.BuildPath([.. pathSegments]);

            // Fall back to the folder name without its number prefix. The home page has no
            // folder name; ContentService gives it the site title instead.
            var fallbackTitle = string.IsNullOrEmpty(relativePath)
                ? string.Empty
                : UrlBuilder.ToTitle(Path.GetFileName(relativePath));

            var gallery = new Gallery
            {
                Path = relativePath,
                Slug = slug,
                Title = directoryMetadata.Title ?? fallbackTitle,
                Description = directoryMetadata.Description,
                Template = directoryMetadata.Template,
                Sort = directoryMetadata.Sort,
                Filter = directoryMetadata.Filter,
                Cover = directoryMetadata.Cover,
                DataSources = directoryMetadata.DataSources,
                Images = []
            };

            foreach (var imageFile in imageFiles)
            {
                var imageRelativePath = string.IsNullOrEmpty(relativePath)
                    ? Path.GetFileName(imageFile)
                    : Path.Combine(relativePath, Path.GetFileName(imageFile));

                var sourceImage = new SourceImage
                {
                    SourcePath = imageFile,
                    RelativePath = imageRelativePath,
                    FileName = Path.GetFileName(imageFile),
                    FileSize = new FileInfo(imageFile).Length,
                    LastModified = File.GetLastWriteTimeUtc(imageFile),
                    Gallery = relativePath
                };

                images.Add(sourceImage);
            }

            galleries.Add(gallery);
        }

        // Recursively scan subdirectories (skip folders starting with _)
        var subdirectories = Directory.GetDirectories(currentDirectory);
        foreach (var subdirectory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subdirName = Path.GetFileName(subdirectory);

            // Skip folders starting with underscore (convention: _assets, _drafts, _static, etc.)
            if (subdirName.StartsWith('_'))
            {
                // Special case: _images folder at root level - scan images but don't create galleries
                if (string.IsNullOrEmpty(relativePath) &&
                    subdirName.Equals(ProjectPaths.SharedImages, StringComparison.OrdinalIgnoreCase))
                {
                    var sharedImageCount = ScanSharedImagesRecursive(subdirectory, baseDirectory, images, cancellationToken);
                    if (sharedImageCount > 0)
                    {
                        LogSharedImagesFound(logger, sharedImageCount, ProjectPaths.SharedImages);
                    }
                }

                continue;
            }

            var subdirRelativePath = string.IsNullOrEmpty(relativePath)
                ? subdirName
                : Path.Combine(relativePath, subdirName);

            await ScanDirectoryAsync(baseDirectory, subdirRelativePath, images, galleries, cancellationToken);
        }
    }

    /// <summary>
    /// Recursively scans the shared images folder for images without creating galleries.
    /// </summary>
    /// <param name="sharedDirectory">Current directory to scan.</param>
    /// <param name="baseDirectory">The source root directory (parent of _images).</param>
    /// <param name="images">List to add discovered images to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Number of images found.</returns>
    private static int ScanSharedImagesRecursive(
        string sharedDirectory,
        string baseDirectory,
        List<SourceImage> images,
        CancellationToken cancellationToken)
    {
        var count = 0;

        // Scan images in this directory
        var imageFiles = Directory.EnumerateFiles(sharedDirectory)
            .Where(f => SupportedImageExtensions.IsSupported(Path.GetExtension(f)));

        foreach (var imageFile in imageFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Build relative path from source root (e.g., "_images/subfolder/photo.jpg")
            var relativePath = Path.GetRelativePath(baseDirectory, imageFile);

            images.Add(new SourceImage
            {
                SourcePath = imageFile,
                RelativePath = relativePath,
                FileName = Path.GetFileName(imageFile),
                FileSize = new FileInfo(imageFile).Length,
                LastModified = File.GetLastWriteTimeUtc(imageFile),
                Gallery = ProjectPaths.SharedImages  // Mark as shared image
            });

            count++;
        }

        // Recursively scan subdirectories
        foreach (var subdir in Directory.GetDirectories(sharedDirectory))
        {
            count += ScanSharedImagesRecursive(subdir, baseDirectory, images, cancellationToken);
        }

        return count;
    }

    private async Task<DirectoryMetadata> LoadGalleryMetadataAsync(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(directoryPath, RevelaParser.IndexFileName);
        if (!File.Exists(indexPath))
        {
            return DirectoryMetadata.Empty;
        }

        return await revelaParser.ParseFileAsync(indexPath, cancellationToken);
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanning content directory: {Directory}")]
    private static partial void LogScanningDirectory(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan complete: {ImageCount} images, {GalleryCount} galleries")]
    private static partial void LogScanComplete(ILogger logger, int imageCount, int galleryCount);

    [LoggerMessage(Level = LogLevel.Information, Message = "Found {Count} shared images in {Folder}/")]
    private static partial void LogSharedImagesFound(ILogger logger, int count, string folder);
}

