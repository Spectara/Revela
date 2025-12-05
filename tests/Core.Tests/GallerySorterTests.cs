using Spectara.Revela.Features.Generate.Services;

namespace Spectara.Revela.Core.Tests;

/// <summary>
/// Tests for GallerySorter natural sorting functionality
/// </summary>
[TestClass]
public sealed class GallerySorterTests
{
    #region ExtractDisplayName Tests

    [TestMethod]
    public void ExtractDisplayName_WithSortPrefix_ShouldRemovePrefix()
    {
        // Arrange & Act
        var result = GallerySorter.ExtractDisplayName("01 Events");

        // Assert
        Assert.AreEqual("Events", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithTwoDigitPrefix_ShouldRemovePrefix()
    {
        // Arrange & Act
        var result = GallerySorter.ExtractDisplayName("99 Last Item");

        // Assert
        Assert.AreEqual("Last Item", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithSingleDigitPrefix_ShouldRemovePrefix()
    {
        // Arrange & Act
        var result = GallerySorter.ExtractDisplayName("5 Fifth");

        // Assert
        Assert.AreEqual("Fifth", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithYearPrefix_ShouldKeepAsIs()
    {
        // Arrange - Year prefixes (4 digits) should NOT be stripped
        var input = "2024 Summer";

        // Act
        var result = GallerySorter.ExtractDisplayName(input);

        // Assert - Year stays because regex only matches 1-2 digits
        Assert.AreEqual("2024 Summer", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithNoPrefix_ShouldReturnOriginal()
    {
        // Arrange & Act
        var result = GallerySorter.ExtractDisplayName("Events");

        // Assert
        Assert.AreEqual("Events", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithThreeDigitPrefix_ShouldKeepAsIs()
    {
        // Arrange - Only 1-2 digit prefixes are stripped
        var input = "123 Test";

        // Act
        var result = GallerySorter.ExtractDisplayName(input);

        // Assert
        Assert.AreEqual("123 Test", result);
    }

    [TestMethod]
    public void ExtractDisplayName_WithNullInput_ShouldThrow()
    {
        // Act & Assert - ArgumentNullException is derived from ArgumentException
        Assert.Throws<ArgumentException>(() =>
            GallerySorter.ExtractDisplayName(null!));
    }

    [TestMethod]
    public void ExtractDisplayName_WithEmptyInput_ShouldThrow()
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() =>
            GallerySorter.ExtractDisplayName(string.Empty));
    }

    #endregion

    #region NaturalComparer Tests

    [TestMethod]
    public void NaturalComparer_ShouldSortNumerically()
    {
        // Arrange
        var items = new[] { "item10", "item2", "item1", "item20" };

        // Act
        var sorted = items.OrderBy(x => x, GallerySorter.NaturalComparer).ToArray();

        // Assert
        Assert.AreEqual("item1", sorted[0]);
        Assert.AreEqual("item2", sorted[1]);
        Assert.AreEqual("item10", sorted[2]);
        Assert.AreEqual("item20", sorted[3]);
    }

    [TestMethod]
    public void NaturalComparer_ShouldSortFolderNamesNaturally()
    {
        // Arrange
        var folders = new[] { "01 Events", "10 Portraits", "2 Wedding" };

        // Act
        var sorted = folders.OrderBy(x => x, GallerySorter.NaturalComparer).ToArray();

        // Assert
        Assert.AreEqual("01 Events", sorted[0]);
        Assert.AreEqual("2 Wedding", sorted[1]);
        Assert.AreEqual("10 Portraits", sorted[2]);
    }

    #endregion

    #region SortNatural Extension Tests

    [TestMethod]
    public void SortNatural_ShouldSortStringsNaturally()
    {
        // Arrange
        var items = new[] { "photo10.jpg", "photo2.jpg", "photo1.jpg" };

        // Act
        var sorted = items.SortNatural(x => x).ToArray();

        // Assert
        Assert.AreEqual("photo1.jpg", sorted[0]);
        Assert.AreEqual("photo2.jpg", sorted[1]);
        Assert.AreEqual("photo10.jpg", sorted[2]);
    }

    [TestMethod]
    public void SortNatural_WithDescending_ShouldSortReversed()
    {
        // Arrange
        var items = new[] { "photo1.jpg", "photo2.jpg", "photo10.jpg" };

        // Act
        var sorted = items.SortNatural(x => x, descending: true).ToArray();

        // Assert
        Assert.AreEqual("photo10.jpg", sorted[0]);
        Assert.AreEqual("photo2.jpg", sorted[1]);
        Assert.AreEqual("photo1.jpg", sorted[2]);
    }

    [TestMethod]
    public void SortPathsNatural_ShouldSortByFileName()
    {
        // Arrange
        var paths = new[]
        {
            "/photos/gallery/img10.jpg",
            "/photos/gallery/img2.jpg",
            "/photos/gallery/img1.jpg"
        };

        // Act
        var sorted = paths.SortPathsNatural().ToArray();

        // Assert
        Assert.IsTrue(sorted[0].EndsWith("img1.jpg", StringComparison.Ordinal));
        Assert.IsTrue(sorted[1].EndsWith("img2.jpg", StringComparison.Ordinal));
        Assert.IsTrue(sorted[2].EndsWith("img10.jpg", StringComparison.Ordinal));
    }

    #endregion
}
