using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Parsing;
using Spectara.Revela.Sdk;

namespace Spectara.Revela.Commands.Generate.Scanning;

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
public sealed partial class ContentScanner(
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
        var markdowns = new List<SourceMarkdown>();
        var galleries = new List<Gallery>();

        // Scan root directory
        await ScanDirectoryAsync(sourceDirectory, string.Empty, images, markdowns, galleries, cancellationToken);

        LogScanComplete(logger, images.Count, galleries.Count);

        return new ContentTree
        {
            Images = images,
            Markdowns = markdowns,
            Galleries = galleries
        };
    }

    private async Task ScanDirectoryAsync(
        string baseDirectory,
        string relativePath,
        List<SourceImage> images,
        List<SourceMarkdown> markdowns,
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

        // Find markdown files (*.md) excluding _index.revela
        var markdownFiles = Directory.EnumerateFiles(currentDirectory, "*.md")
            .Where(f => !Path.GetFileName(f).Equals(RevelaParser.IndexFileName, StringComparison.OrdinalIgnoreCase))
            .ToList();

        // Check for _index.revela (gallery metadata or standalone page)
        var hasIndexFile = File.Exists(Path.Combine(currentDirectory, RevelaParser.IndexFileName));

        // Create gallery if directory has images OR has _index.md (text-only pages)
        if (imageFiles.Count > 0 || (hasIndexFile && !string.IsNullOrEmpty(relativePath)))
        {
            // This directory contains images or is a text-only page
            var directoryMetadata = await LoadGalleryMetadataAsync(currentDirectory, cancellationToken);

            // Build URL-safe slug from path segments
            var pathSegments = string.IsNullOrEmpty(relativePath)
                ? []
                : relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            var slug = UrlBuilder.BuildPath([.. pathSegments]);

            // Use directory name as fallback title (without number prefix)
            var directoryName = string.IsNullOrEmpty(relativePath)
                ? "Home"
                : Path.GetFileName(relativePath);
            var fallbackTitle = UrlBuilder.ToTitle(directoryName);

            var gallery = new Gallery
            {
                Name = directoryName,
                Path = relativePath,
                Slug = slug,
                Title = directoryMetadata.Title ?? fallbackTitle,
                Description = directoryMetadata.Description,
                Template = directoryMetadata.Template,
                Sort = directoryMetadata.Sort,
                Filter = directoryMetadata.Filter,
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

            // Add markdown files to the collection
            foreach (var markdownFile in markdownFiles)
            {
                var mdRelativePath = string.IsNullOrEmpty(relativePath)
                    ? Path.GetFileName(markdownFile)
                    : Path.Combine(relativePath, Path.GetFileName(markdownFile));

                var sourceMarkdown = new SourceMarkdown
                {
                    SourcePath = markdownFile,
                    RelativePath = mdRelativePath,
                    FileName = Path.GetFileName(markdownFile),
                    FileSize = new FileInfo(markdownFile).Length,
                    LastModified = File.GetLastWriteTimeUtc(markdownFile),
                    Gallery = relativePath
                };

                markdowns.Add(sourceMarkdown);
            }

            galleries.Add(gallery);
        }

        // Recursively scan subdirectories (skip folders starting with _)
        var subdirectories = Directory.GetDirectories(currentDirectory);
        foreach (var subdirectory in subdirectories)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var subdirName = Path.GetFileName(subdirectory);

            // Skip folders starting with underscore (convention: _assets, _drafts, etc.)
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

            await ScanDirectoryAsync(baseDirectory, subdirRelativePath, images, markdowns, galleries, cancellationToken);
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
