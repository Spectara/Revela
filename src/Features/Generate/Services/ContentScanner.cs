namespace Spectara.Revela.Features.Generate.Services;

/// <summary>
/// Scans content directory for images and markdown files
/// </summary>
/// <remarks>
/// Discovers:
/// - Image files (*.jpg, *.jpeg, *.png, *.webp)
/// - Markdown files (_index.md for gallery metadata)
/// - Directory structure (galleries/albums)
///
/// Creates a content tree representing the site structure.
/// </remarks>
public sealed partial class ContentScanner(ILogger<ContentScanner> logger)
{
    private static readonly string[] ImageExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];

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

        LogScanComplete(logger, images.Count, galleries.Count);

        return new ContentTree
        {
            Images = images,
            Galleries = galleries
        };
    }

    private static async Task ScanDirectoryAsync(
        string baseDirectory,
        string relativePath,
        List<SourceImage> images,
        List<Gallery> galleries,
        CancellationToken cancellationToken)
    {
        var currentDirectory = string.IsNullOrEmpty(relativePath)
            ? baseDirectory
            : Path.Combine(baseDirectory, relativePath);

        if (!Directory.Exists(currentDirectory))
        {
            return;
        }

        // Find images in current directory
        var imageFiles = Directory.EnumerateFiles(currentDirectory)
            .Where(f => ImageExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .ToList();

        if (imageFiles.Count > 0)
        {
            // This directory contains images - it's a gallery
            var galleryMetadata = await LoadGalleryMetadataAsync(currentDirectory, cancellationToken);

            var gallery = new Gallery
            {
                Name = string.IsNullOrEmpty(relativePath)
                    ? "Root"
                    : Path.GetFileName(relativePath),
                Path = relativePath,
                Title = galleryMetadata?.Title,
                Description = galleryMetadata?.Description,
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

        // Recursively scan subdirectories
        var subdirectories = Directory.GetDirectories(currentDirectory);
        foreach (var subdirectory in subdirectories)
        {
            var subdirName = Path.GetFileName(subdirectory);
            var subdirRelativePath = string.IsNullOrEmpty(relativePath)
                ? subdirName
                : Path.Combine(relativePath, subdirName);

            await ScanDirectoryAsync(baseDirectory, subdirRelativePath, images, galleries, cancellationToken);
        }
    }

    private static async Task<GalleryMetadata?> LoadGalleryMetadataAsync(
        string directoryPath,
        CancellationToken cancellationToken)
    {
        var indexPath = Path.Combine(directoryPath, "_index.md");
        if (!File.Exists(indexPath))
        {
            return null;
        }

        try
        {
            var content = await File.ReadAllTextAsync(indexPath, cancellationToken);

            // TODO: Parse frontmatter (YAML between --- delimiters)
            // For now, just extract title from first # heading
            var lines = content.Split('\n');
            var title = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# ", StringComparison.Ordinal))
                ?.TrimStart('#', ' ')
                .Trim();

            return new GalleryMetadata
            {
                Title = title,
                Description = null
            };
        }
        catch
        {
            // Log but don't fail - gallery metadata is optional
            return null;
        }
    }

    // High-performance logging with LoggerMessage source generator
    [LoggerMessage(Level = LogLevel.Information, Message = "Scanning content directory: {Directory}")]
    static partial void LogScanningDirectory(ILogger logger, string directory);

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan complete: {ImageCount} images, {GalleryCount} galleries")]
    static partial void LogScanComplete(ILogger logger, int imageCount, int galleryCount);
}

/// <summary>
/// Content tree representing scanned content
/// </summary>
public sealed class ContentTree
{
    public required IReadOnlyList<SourceImage> Images { get; init; }
    public required IReadOnlyList<Gallery> Galleries { get; init; }
}

/// <summary>
/// Source image before processing
/// </summary>
public sealed class SourceImage
{
    public required string SourcePath { get; init; }
    public required string RelativePath { get; init; }
    public required string FileName { get; init; }
    public required long FileSize { get; init; }
    public required DateTime LastModified { get; init; }
    public required string Gallery { get; init; }
}

/// <summary>
/// Gallery (directory containing images)
/// </summary>
public sealed class Gallery
{
    public required string Name { get; init; }
    public required string Path { get; init; }
    public string? Title { get; init; }
    public string? Description { get; init; }
    public required IReadOnlyList<SourceImage> Images { get; init; }
}

/// <summary>
/// Gallery metadata from _index.md
/// </summary>
public sealed class GalleryMetadata
{
    public string? Title { get; init; }
    public string? Description { get; init; }
}
