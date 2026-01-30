using Spectara.Revela.Commands.Generate.Services;
using Spectara.Revela.Core.Configuration;

namespace Spectara.Revela.Commands.Tests.Generate.Services;

/// <summary>
/// Tests for <see cref="ManifestService"/> static helper methods.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ManifestServiceTests
{
    #region ComputeConfigHash Tests

    [TestMethod]
    public void ComputeConfigHash_SameSizesAndFormats_ReturnsSameHash()
    {
        // Arrange
        var sizes1 = new[] { 320, 640, 1024, 1920 };
        var sizes2 = new[] { 320, 640, 1024, 1920 };
        var formats = new Dictionary<string, int> { ["jpg"] = 90, ["webp"] = 85 };

        // Act
        var hash1 = ManifestService.ComputeConfigHash(sizes1, formats);
        var hash2 = ManifestService.ComputeConfigHash(sizes2, formats);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeConfigHash_DifferentSizes_ReturnsDifferentHash()
    {
        // Arrange
        var sizes1 = new[] { 320, 640, 1024 };
        var sizes2 = new[] { 320, 640, 1920 };
        var formats = new Dictionary<string, int> { ["jpg"] = 90 };

        // Act
        var hash1 = ManifestService.ComputeConfigHash(sizes1, formats);
        var hash2 = ManifestService.ComputeConfigHash(sizes2, formats);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeConfigHash_DifferentFormats_ReturnsDifferentHash()
    {
        // Arrange
        var sizes = new[] { 640, 1024 };
        var formats1 = new Dictionary<string, int> { ["jpg"] = 90 };
        var formats2 = new Dictionary<string, int> { ["jpg"] = 85 };

        // Act
        var hash1 = ManifestService.ComputeConfigHash(sizes, formats1);
        var hash2 = ManifestService.ComputeConfigHash(sizes, formats2);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeConfigHash_SizesInDifferentOrder_ReturnsSameHash()
    {
        // Arrange - Order shouldn't matter
        var sizes1 = new[] { 1920, 640, 320, 1024 };
        var sizes2 = new[] { 320, 640, 1024, 1920 };
        var formats = new Dictionary<string, int> { ["jpg"] = 90 };

        // Act
        var hash1 = ManifestService.ComputeConfigHash(sizes1, formats);
        var hash2 = ManifestService.ComputeConfigHash(sizes2, formats);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeConfigHash_ReturnsConsistentLength()
    {
        // Arrange
        var sizes = new[] { 640, 1024 };
        var formats = new Dictionary<string, int> { ["jpg"] = 90, ["webp"] = 85, ["avif"] = 80 };

        // Act
        var hash = ManifestService.ComputeConfigHash(sizes, formats);

        // Assert - Should be 12 characters (first 12 hex chars of SHA256)
        Assert.AreEqual(12, hash.Length);
        Assert.IsTrue(hash.All(c => char.IsLetterOrDigit(c)));
    }

    #endregion

    #region ComputeScanConfigHash Tests

    [TestMethod]
    public void ComputeScanConfigHash_SameConfig_ReturnsSameHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 100);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_DifferentStrategy_ReturnsDifferentHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.None, 100, 100);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_DifferentMinWidth_ReturnsDifferentHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 200, 100);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_DifferentMinHeight_ReturnsDifferentHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 100, 200);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_ReturnsConsistentLength()
    {
        // Arrange & Act
        var hash = ManifestService.ComputeScanConfigHash(PlaceholderStrategy.CssHash, 0, 0);

        // Assert - Should be 12 characters
        Assert.AreEqual(12, hash.Length);
        Assert.IsTrue(hash.All(c => char.IsLetterOrDigit(c)));
    }

    #endregion
}
