using Spectara.Revela.Features.Generate.Services;
using Spectara.Revela.Sdk.Configuration;

namespace Spectara.Revela.Tests.Commands.Generate.Services;

/// <summary>
/// Tests for <see cref="ManifestService"/> static helper methods.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class ManifestServiceTests
{
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

