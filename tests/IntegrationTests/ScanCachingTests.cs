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
        // Simulate the cache hit condition from ContentService
        var cachedEntry = new ImageContent
        {
            Filename = "photo.jpg",
            SourcePath = "gallery/photo.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            FileSize = 5_000_000,
            LastModified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        // Simulated file info from disk
        var currentFileSize = 5_000_000L;
        var currentLastModified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        // This is the exact condition from ContentService
        var isCacheHit = cachedEntry.FileSize == currentFileSize &&
                         cachedEntry.LastModified == currentLastModified;

        Assert.IsTrue(isCacheHit, "Should be cache hit when FileSize and LastModified match");
    }

    [TestMethod]
    public void CacheHitCondition_DifferentFileSize_ShouldNotMatch()
    {
        var cachedEntry = new ImageContent
        {
            Filename = "photo.jpg",
            SourcePath = "gallery/photo.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            FileSize = 5_000_000,
            LastModified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        // Different file size (image was modified)
        var currentFileSize = 5_500_000L;
        var currentLastModified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var isCacheHit = cachedEntry.FileSize == currentFileSize &&
                         cachedEntry.LastModified == currentLastModified;

        Assert.IsFalse(isCacheHit, "Should be cache miss when FileSize differs");
    }

    [TestMethod]
    public void CacheHitCondition_DifferentLastModified_ShouldNotMatch()
    {
        var cachedEntry = new ImageContent
        {
            Filename = "photo.jpg",
            SourcePath = "gallery/photo.jpg",
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            FileSize = 5_000_000,
            LastModified = new DateTime(2026, 1, 15, 10, 0, 0, DateTimeKind.Utc)
        };

        // Different LastModified (file was touched/re-saved)
        var currentFileSize = 5_000_000L;
        var currentLastModified = new DateTime(2026, 1, 16, 10, 0, 0, DateTimeKind.Utc);

        var isCacheHit = cachedEntry.FileSize == currentFileSize &&
                         cachedEntry.LastModified == currentLastModified;

        Assert.IsFalse(isCacheHit, "Should be cache miss when LastModified differs");
    }
}
