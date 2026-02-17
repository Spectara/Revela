using Spectara.Revela.Plugin.Source.OneDrive.Models;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Tests.Shared;

/// <summary>
/// Static factory methods for creating test data.
/// </summary>
/// <remarks>
/// Follows Microsoft conventions for test data creation.
/// All methods return valid objects with sensible defaults.
/// Override specific properties as needed for your test.
/// </remarks>
public static class TestData
{
    #region ImageContent

    /// <summary>
    /// Creates a default <see cref="ImageContent"/> for testing.
    /// </summary>
    /// <param name="filename">The filename (defaults to "test.jpg").</param>
    /// <param name="exif">Optional EXIF data.</param>
    /// <param name="dateTaken">Optional date taken.</param>
    /// <param name="width">Image width (defaults to 1920).</param>
    /// <param name="height">Image height (defaults to 1080).</param>
    /// <returns>A valid ImageContent instance.</returns>
    public static ImageContent Image(
        string filename = "test.jpg",
        ExifData? exif = null,
        DateTime? dateTaken = null,
        int width = 1920,
        int height = 1080) => new()
        {
            Filename = filename,
            Width = width,
            Height = height,
            Sizes = [width],
            Exif = exif,
            DateTaken = dateTaken
        };

    /// <summary>
    /// Creates multiple <see cref="ImageContent"/> instances.
    /// </summary>
    /// <param name="count">Number of images to create.</param>
    /// <param name="exif">Optional EXIF data to apply to all images.</param>
    /// <returns>A dictionary of images keyed by filename.</returns>
    public static Dictionary<string, ImageContent> Images(int count, ExifData? exif = null)
    {
        var images = new Dictionary<string, ImageContent>();
        for (var i = 0; i < count; i++)
        {
            var filename = $"img{i}.jpg";
            images[filename] = Image(filename, exif ?? Exif());
        }
        return images;
    }

    #endregion

    #region ExifData

    /// <summary>
    /// Creates default <see cref="ExifData"/> for testing.
    /// </summary>
    /// <param name="fNumber">F-number/aperture (defaults to 2.8).</param>
    /// <param name="iso">ISO value (defaults to 100).</param>
    /// <param name="focalLength">Focal length in mm (defaults to 50).</param>
    /// <param name="model">Camera model (defaults to "Test Camera").</param>
    /// <param name="lensModel">Lens model.</param>
    /// <param name="exposureTime">Exposure time in seconds.</param>
    /// <returns>A valid ExifData instance.</returns>
    public static ExifData Exif(
        double fNumber = 2.8,
        int iso = 100,
        double focalLength = 50,
        string model = "Test Camera",
        string? lensModel = null,
        double? exposureTime = null) => new()
        {
            FNumber = fNumber,
            Iso = iso,
            FocalLength = focalLength,
            Model = model,
            LensModel = lensModel ?? "Test Lens",
            ExposureTime = exposureTime
        };

    #endregion

    #region OneDriveItem

    /// <summary>
    /// Creates a default <see cref="OneDriveItem"/> for testing.
    /// </summary>
    /// <param name="name">File name (defaults to "photo.jpg").</param>
    /// <param name="downloadUrl">Download URL.</param>
    /// <param name="lastModified">Last modified time.</param>
    /// <param name="size">File size in bytes (defaults to 1024).</param>
    /// <param name="isFolder">Whether this is a folder.</param>
    /// <param name="parentPath">Parent path for nested items.</param>
    /// <returns>A valid OneDriveItem instance.</returns>
    public static OneDriveItem OneDriveItem(
        string name = "photo.jpg",
        string? downloadUrl = null,
        DateTime? lastModified = null,
        long size = 1024,
        bool isFolder = false,
        string? parentPath = null)
    {
        // Folders don't have download URLs, files get a default if not specified
        var resolvedDownloadUrl = isFolder ? null : downloadUrl ?? $"https://cdn.example.com/{name}";

        return new OneDriveItem
        {
            Id = Guid.NewGuid().ToString(),
            Name = name,
            DownloadUrl = resolvedDownloadUrl,
            Size = size,
            LastModified = lastModified ?? DateTime.UtcNow,
            IsFolder = isFolder,
            ParentPath = parentPath ?? string.Empty
        };
    }

    /// <summary>
    /// Creates a <see cref="OneDriveItem"/> representing a folder.
    /// </summary>
    /// <param name="name">Folder name.</param>
    /// <param name="parentPath">Parent path for nested folders.</param>
    /// <returns>A valid OneDriveItem folder instance.</returns>
    public static OneDriveItem OneDriveFolder(string name = "Gallery", string? parentPath = null) =>
        OneDriveItem(name, downloadUrl: null, isFolder: true, parentPath: parentPath);

    #endregion

    #region DownloadStatistics

    /// <summary>
    /// Creates default <see cref="DownloadStatistics"/> for testing.
    /// </summary>
    /// <param name="newFiles">Number of new files.</param>
    /// <param name="modifiedFiles">Number of modified files.</param>
    /// <param name="unchangedFiles">Number of unchanged files.</param>
    /// <param name="orphanedFiles">Number of orphaned files.</param>
    /// <param name="totalDownloadSize">Total size to download in bytes.</param>
    /// <param name="totalOrphanedSize">Total size of orphaned files in bytes.</param>
    /// <returns>A valid DownloadStatistics instance.</returns>
    public static DownloadStatistics DownloadStatistics(
        int newFiles = 0,
        int modifiedFiles = 0,
        int unchangedFiles = 0,
        int orphanedFiles = 0,
        long totalDownloadSize = 0,
        long totalOrphanedSize = 0) => new()
        {
            NewFiles = newFiles,
            ModifiedFiles = modifiedFiles,
            UnchangedFiles = unchangedFiles,
            OrphanedFiles = orphanedFiles,
            TotalDownloadSize = totalDownloadSize,
            TotalOrphanedSize = totalOrphanedSize
        };

    #endregion

    #region DownloadItem

    /// <summary>
    /// Creates a default <see cref="DownloadItem"/> for testing.
    /// </summary>
    /// <param name="remoteItem">Remote OneDrive item.</param>
    /// <param name="status">File status (defaults to New).</param>
    /// <param name="reason">Status reason (defaults to "Test").</param>
    /// <param name="localFile">Local file info.</param>
    /// <returns>A valid DownloadItem instance.</returns>
    public static DownloadItem DownloadItem(
        OneDriveItem? remoteItem = null,
        FileStatus status = FileStatus.New,
        string reason = "Test",
        FileInfo? localFile = null) => new()
        {
            RemoteItem = remoteItem ?? OneDriveItem(),
            Status = status,
            Reason = reason,
            LocalFile = localFile
        };

    #endregion

    #region DownloadAnalysis

    /// <summary>
    /// Creates a default <see cref="DownloadAnalysis"/> for testing.
    /// </summary>
    /// <param name="statistics">Download statistics.</param>
    /// <param name="items">All analyzed items.</param>
    /// <param name="orphanedFiles">Orphaned file infos.</param>
    /// <returns>A valid DownloadAnalysis instance.</returns>
    public static DownloadAnalysis DownloadAnalysis(
        DownloadStatistics? statistics = null,
        IReadOnlyList<DownloadItem>? items = null,
        IReadOnlyList<FileInfo>? orphanedFiles = null) => new()
        {
            Statistics = statistics ?? DownloadStatistics(),
            Items = items ?? [],
            OrphanedFiles = orphanedFiles ?? []
        };

    #endregion
}
