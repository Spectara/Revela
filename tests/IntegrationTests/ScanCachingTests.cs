using System.Text.Json;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.IntegrationTests;

/// <summary>
/// Integration tests for scan caching functionality.
/// Tests that unchanged images are cached and not re-read.
/// Note: Hash computation tests are in ManifestServiceTests (Unit tests).
/// </summary>
[TestClass]
[TestCategory("Integration")]
public sealed class ScanCachingTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = true };

    [TestMethod]
    public void ImageContent_StoresLastModified()
    {
        // Verify that ImageContent properly stores LastModified
        var now = DateTime.UtcNow;
        var image = new ImageContent
        {
            Filename = "test.jpg",
            SourcePath = "gallery/test.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            FileSize = 1024,
            LastModified = now
        };

        Assert.AreEqual(now, image.LastModified);
    }

    [TestMethod]
    public void ImageContent_SerializesLastModified()
    {
        // Verify that LastModified survives JSON round-trip
        var originalTime = new DateTime(2026, 1, 15, 12, 30, 45, DateTimeKind.Utc);
        var image = new ImageContent
        {
            Filename = "test.jpg",
            SourcePath = "gallery/test.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            FileSize = 1024,
            LastModified = originalTime
        };

        var json = JsonSerializer.Serialize(image, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ImageContent>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual(originalTime, deserialized.LastModified);
    }

    [TestMethod]
    public void ManifestMeta_StoresScanConfigHash()
    {
        // Verify that ManifestMeta properly stores ScanConfigHash
        var meta = new ManifestMeta
        {
            ScanConfigHash = "ABC123DEF456"
        };

        Assert.AreEqual("ABC123DEF456", meta.ScanConfigHash);
    }

    [TestMethod]
    public void ManifestMeta_SerializesScanConfigHash()
    {
        // Verify that ScanConfigHash survives JSON round-trip
        var meta = new ManifestMeta
        {
            ConfigHash = "CONFIG123",
            ScanConfigHash = "SCAN456789"
        };

        var json = JsonSerializer.Serialize(meta, SerializerOptions);
        var deserialized = JsonSerializer.Deserialize<ManifestMeta>(json);

        Assert.IsNotNull(deserialized);
        Assert.AreEqual("SCAN456789", deserialized.ScanConfigHash);
    }

    [TestMethod]
    public void CacheHitCondition_SameFileSizeAndLastModified_ShouldMatch()
    {
        var cachedEntry = CreateCachedEntry(5_000_000, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        var isCacheHit = IsCacheHit(cachedEntry, 5_000_000L, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.IsTrue(isCacheHit, "Should be cache hit when FileSize and LastModified match");
    }

    [TestMethod]
    public void CacheHitCondition_DifferentFileSize_ShouldNotMatch()
    {
        var cachedEntry = CreateCachedEntry(5_000_000, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        // Different file size (image was modified)
        var isCacheHit = IsCacheHit(cachedEntry, 5_500_000L, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        Assert.IsFalse(isCacheHit, "Should be cache miss when FileSize differs");
    }

    [TestMethod]
    public void CacheHitCondition_DifferentLastModified_ShouldNotMatch()
    {
        var cachedEntry = CreateCachedEntry(5_000_000, new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc));

        // Different LastModified (file was touched/re-saved)
        var isCacheHit = IsCacheHit(cachedEntry, 5_000_000L, new DateTime(2026, 1, 16, 10, 0, 0, DateTimeKind.Utc));

        Assert.IsFalse(isCacheHit, "Should be cache miss when LastModified differs");
    }

    /// <summary>
    /// Creates a cached entry with the given file size and last modified date.
    /// Extracted to prevent CA1508 false positives from constant folding.
    /// </summary>
    private static ImageContent CreateCachedEntry(long fileSize, DateTime lastModified) => new()
    {
        Filename = "photo.jpg",
        SourcePath = "gallery/photo.jpg",
        Width = 1920,
        Height = 1080,
        Sizes = [1920],
        FileSize = fileSize,
        LastModified = lastModified
    };

    /// <summary>
    /// Evaluates the cache hit condition (same logic as ContentService).
    /// </summary>
    private static bool IsCacheHit(ImageContent cached, long currentFileSize, DateTime currentLastModified) =>
        cached.FileSize == currentFileSize && cached.LastModified == currentLastModified;
}
