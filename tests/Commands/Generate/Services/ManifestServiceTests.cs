using Spectara.Revela.Features.Generate.Services;

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
        var hash1 = ManifestService.ComputeScanConfigHash(100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(100, 100);

        // Assert
        Assert.AreEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_DifferentMinWidth_ReturnsDifferentHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(200, 100);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_DifferentMinHeight_ReturnsDifferentHash()
    {
        // Arrange & Act
        var hash1 = ManifestService.ComputeScanConfigHash(100, 100);
        var hash2 = ManifestService.ComputeScanConfigHash(100, 200);

        // Assert
        Assert.AreNotEqual(hash1, hash2);
    }

    [TestMethod]
    public void ComputeScanConfigHash_ReturnsConsistentLength()
    {
        // Arrange & Act
        var hash = ManifestService.ComputeScanConfigHash(0, 0);

        // Assert - Should be 12 characters
        Assert.AreEqual(12, hash.Length);
        Assert.IsTrue(hash.All(c => char.IsLetterOrDigit(c)));
    }

    #endregion
}

