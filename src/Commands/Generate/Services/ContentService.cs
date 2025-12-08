using Spectara.Revela.Commands.Generate.Abstractions;
using Spectara.Revela.Commands.Generate.Building;
using Spectara.Revela.Commands.Generate.Models;
using Spectara.Revela.Commands.Generate.Models.Manifest;
using Spectara.Revela.Commands.Generate.Models.Results;
using Spectara.Revela.Commands.Generate.Scanning;

namespace Spectara.Revela.Commands.Generate.Services;

/// <summary>
/// Service for content scanning and gallery/navigation building.
/// </summary>
/// <remarks>
/// <para>
/// Scans the source directory to discover galleries, images, and navigation structure.
/// Updates the manifest with gallery and navigation data.
/// </para>
/// </remarks>
public sealed partial class ContentService(
    ContentScanner contentScanner,
    NavigationBuilder navigationBuilder,
    IManifestRepository manifestRepository,
    ILogger<ContentService> logger) : IContentService
{
    /// <summary>Fixed source directory (convention over configuration)</summary>
    private const string SourceDirectory = "source";

    /// <inheritdoc />
    public async Task<ContentResult> ScanAsync(
        IProgress<ContentProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate source directory exists
            if (!Directory.Exists(SourceDirectory))
            {
                return new ContentResult
                {
                    Success = false,
                    ErrorMessage = $"Source directory not found: {SourceDirectory}"
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
            var content = await contentScanner.ScanAsync(SourceDirectory, cancellationToken);

            progress?.Report(new ContentProgress
            {
                Status = "Building navigation...",
                GalleriesFound = content.Galleries.Count,
                ImagesFound = content.Images.Count
            });

            // Build navigation tree
            var navigation = await navigationBuilder.BuildAsync(SourceDirectory, cancellationToken: cancellationToken);

            // Convert to manifest entries
            var galleryEntries = ConvertGalleries(content.Galleries);
            var navigationEntries = ConvertNavigation(navigation);

            // Update manifest
            manifestRepository.SetGalleries(galleryEntries);
            manifestRepository.SetNavigation(navigationEntries);
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

            return new ContentResult
            {
                Success = true,
                GalleryCount = content.Galleries.Count,
                ImageCount = content.Images.Count
            };
        }
        catch (Exception ex)
        {
            LogScanFailed(logger, ex);
            return new ContentResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }

    #region Conversion Helpers

    private static List<GalleryManifestEntry> ConvertGalleries(IReadOnlyList<Gallery> galleries)
    {
        var entries = new List<GalleryManifestEntry>();
        foreach (var gallery in galleries)
        {
            entries.Add(ConvertGallery(gallery));
        }
        return entries;
    }

    private static GalleryManifestEntry ConvertGallery(Gallery gallery)
    {
        return new GalleryManifestEntry
        {
            Path = gallery.Path,
            Slug = gallery.Slug,
            Name = gallery.Name,
            Title = gallery.Title,
            Description = gallery.Description,
            Cover = gallery.Cover,
            Date = gallery.Date,
            Featured = gallery.Featured,
            Weight = gallery.Weight,
            ImagePaths = [.. gallery.Images.Select(i => i.SourcePath)],
            SubGalleries = [.. gallery.SubGalleries.Select(ConvertGallery)]
        };
    }

    private static List<NavigationManifestEntry> ConvertNavigation(IReadOnlyList<NavigationItem> navigation)
    {
        var entries = new List<NavigationManifestEntry>();
        foreach (var item in navigation)
        {
            entries.Add(ConvertNavigationItem(item));
        }
        return entries;
    }

    private static NavigationManifestEntry ConvertNavigationItem(NavigationItem item)
    {
        return new NavigationManifestEntry
        {
            Text = item.Text,
            Url = item.Url,
            Description = item.Description,
            Hidden = item.Hidden,
            Children = [.. item.Children.Select(ConvertNavigationItem)]
        };
    }

    #endregion

    #region Logging

    [LoggerMessage(Level = LogLevel.Information, Message = "Scan completed: {GalleryCount} galleries, {ImageCount} images")]
    private static partial void LogScanCompleted(ILogger logger, int galleryCount, int imageCount);

    [LoggerMessage(Level = LogLevel.Error, Message = "Scan failed")]
    private static partial void LogScanFailed(ILogger logger, Exception exception);

    #endregion
}
