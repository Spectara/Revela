using Spectara.Revela.Commands.Generate.Building;

namespace Spectara.Revela.Commands.Tests;

/// <summary>
/// Tests for UrlBuilder slug and path generation
/// </summary>
[TestClass]
public sealed class UrlBuilderTests
{
    #region ToSlug Tests

    [TestMethod]
    public void ToSlug_WithSortPrefix_ShouldRemovePrefix()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("01 Events");

        // Assert
        Assert.AreEqual("events", result);
    }

    [TestMethod]
    public void ToSlug_WithSpaces_ShouldConvertToHyphens()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("Summer Trip");

        // Assert
        Assert.AreEqual("summer-trip", result);
    }

    [TestMethod]
    public void ToSlug_WithUpperCase_ShouldConvertToLowercase()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("MyGallery");

        // Assert
        Assert.AreEqual("mygallery", result);
    }

    [TestMethod]
    public void ToSlug_WithDiacritics_ShouldRemoveAccents()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("Café Photos");

        // Assert
        Assert.AreEqual("cafe-photos", result);
    }

    [TestMethod]
    public void ToSlug_WithSpecialChars_ShouldRemoveThem()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("Test & Demo!");

        // Assert
        Assert.AreEqual("test-demo", result);
    }

    [TestMethod]
    public void ToSlug_WithUnderscores_ShouldConvertToHyphens()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("my_gallery_name");

        // Assert
        Assert.AreEqual("my-gallery-name", result);
    }

    [TestMethod]
    public void ToSlug_WithMultipleSpaces_ShouldUseSingleHyphen()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("Too   Many    Spaces");

        // Assert
        Assert.AreEqual("too-many-spaces", result);
    }

    [TestMethod]
    public void ToSlug_WithYear_ShouldKeepNumbers()
    {
        // Arrange & Act
        var result = UrlBuilder.ToSlug("2024 Summer");

        // Assert
        Assert.AreEqual("2024-summer", result);
    }

    [TestMethod]
    public void ToSlug_WithNull_ShouldThrow()
    {
        // Act & Assert - ArgumentNullException is derived from ArgumentException
        Assert.Throws<ArgumentException>(() =>
            UrlBuilder.ToSlug(null!));
    }

    #endregion

    #region BuildPath Tests

    [TestMethod]
    public void BuildPath_WithSingleSegment_ShouldReturnSlugWithTrailingSlash()
    {
        // Arrange & Act
        var result = UrlBuilder.BuildPath("01 Events");

        // Assert
        Assert.AreEqual("events/", result);
    }

    [TestMethod]
    public void BuildPath_WithMultipleSegments_ShouldJoinWithSlashes()
    {
        // Arrange & Act
        var result = UrlBuilder.BuildPath("01 Events", "2024 Wedding");

        // Assert
        Assert.AreEqual("events/2024-wedding/", result);
    }

    [TestMethod]
    public void BuildPath_WithEmptyArray_ShouldReturnEmpty()
    {
        // Arrange & Act
        var result = UrlBuilder.BuildPath();

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void BuildPath_WithEmptySegments_ShouldSkipThem()
    {
        // Arrange & Act
        var result = UrlBuilder.BuildPath("Events", "", "Wedding");

        // Assert
        Assert.AreEqual("events/wedding/", result);
    }

    #endregion

    #region CalculateBasePath Tests

    [TestMethod]
    public void CalculateBasePath_WithSingleLevel_ShouldReturnParentPath()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath("events/");

        // Assert
        Assert.AreEqual("../", result);
    }

    [TestMethod]
    public void CalculateBasePath_WithTwoLevels_ShouldReturnTwoParents()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath("events/2024/");

        // Assert
        Assert.AreEqual("../../", result);
    }

    [TestMethod]
    public void CalculateBasePath_WithEmptyPath_ShouldReturnEmpty()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath(string.Empty);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void CalculateBasePath_WithNull_ShouldReturnEmpty()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath(null!);

        // Assert
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void CalculateBasePath_WithDeepNesting_ShouldReturnCorrectDepth()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath("a/b/c/d/");

        // Assert
        Assert.AreEqual("../../../../", result);
    }

    [TestMethod]
    public void CalculateBasePath_WithoutTrailingSlash_ShouldStillWork()
    {
        // Arrange & Act
        var result = UrlBuilder.CalculateBasePath("events/2024");

        // Assert
        Assert.AreEqual("../../", result);
    }

    #endregion
}
