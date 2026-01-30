using Spectara.Revela.Commands.Generate.Filtering;
using Spectara.Revela.Sdk.Models;
using Spectara.Revela.Sdk.Models.Manifest;

namespace Spectara.Revela.Commands.Tests.Generate.Filtering;

/// <summary>
/// Tests for the <see cref="FilterService"/> class.
/// </summary>
[TestClass]
[TestCategory("Unit")]
public sealed class FilterServiceTests
{
    private static ImageContent CreateTestImage(
        string filename = "test.jpg",
        DateTime? dateTaken = null,
        string? make = null,
        int? iso = null)
    {
        var exif = new ExifData
        {
            Make = make,
            Iso = iso,
            Raw = new Dictionary<string, string>()
        };

        return new ImageContent
        {
            Filename = filename,
            Width = 1920,
            Height = 1080,
            Sizes = [1920],
            DateTaken = dateTaken ?? DateTime.Now,
            Exif = exif
        };
    }

    [TestMethod]
    public void Validate_ValidExpression_ReturnsTrue()
    {
        // Act
        var result = FilterService.Validate("filename == 'test.jpg'");

        // Assert
        Assert.IsTrue(result);
    }

    [TestMethod]
    public void Validate_InvalidExpression_ThrowsException()
    {
        // Act & Assert
        Assert.ThrowsExactly<FilterParseException>(() =>
            FilterService.Validate("filename =="));
    }

    [TestMethod]
    public void Validate_EmptyExpression_ReturnsFalse()
    {
        // Act
        var result = FilterService.Validate("");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void Validate_WhitespaceExpression_ReturnsFalse()
    {
        // Act
        var result = FilterService.Validate("   ");

        // Assert
        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryValidate_ValidExpression_ReturnsTrueNoError()
    {
        // Act
        var result = FilterService.TryValidate("filename == 'test.jpg'", out var error);

        // Assert
        Assert.IsTrue(result);
        Assert.IsNull(error);
    }

    [TestMethod]
    public void TryValidate_InvalidExpression_ReturnsFalseWithError()
    {
        // Act
        var result = FilterService.TryValidate("filename ==", out var error);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.IsNotEmpty(error);
    }

    [TestMethod]
    public void TryValidate_EmptyExpression_ReturnsFalseWithError()
    {
        // Act
        var result = FilterService.TryValidate("", out var error);

        // Assert
        Assert.IsFalse(result);
        Assert.IsNotNull(error);
        Assert.Contains("empty", error, StringComparison.OrdinalIgnoreCase);
    }

    [TestMethod]
    public void Apply_FiltersImagesCorrectly()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", make: "Canon"),
            CreateTestImage(filename: "photo2.jpg", make: "Sony"),
            CreateTestImage(filename: "photo3.jpg", make: "Canon"),
            CreateTestImage(filename: "photo4.jpg", make: "Nikon")
        };

        // Act
        var result = FilterService.Apply(images, "exif.make == 'Canon'").ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.All(i => i.Exif?.Make == "Canon"));
    }

    [TestMethod]
    public void Apply_NoMatches_ReturnsEmpty()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", make: "Canon"),
            CreateTestImage(filename: "photo2.jpg", make: "Sony")
        };

        // Act
        var result = FilterService.Apply(images, "exif.make == 'Nikon'").ToList();

        // Assert
        Assert.IsEmpty(result);
    }

    [TestMethod]
    public void Apply_AllMatch_ReturnsAll()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "photo1.jpg", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "photo2.jpg", dateTaken: new DateTime(2024, 3, 10)),
            CreateTestImage(filename: "photo3.jpg", dateTaken: new DateTime(2024, 9, 20))
        };

        // Act
        var result = FilterService.Apply(images, "year(dateTaken) == 2024").ToList();

        // Assert
        Assert.HasCount(3, result);
    }

    [TestMethod]
    public void Apply_ComplexFilter_WorksCorrectly()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "IMG_001.jpg", make: "Canon", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "IMG_002.jpg", make: "Canon", dateTaken: new DateTime(2023, 6, 15)),
            CreateTestImage(filename: "DSC_001.jpg", make: "Sony", dateTaken: new DateTime(2024, 6, 15)),
            CreateTestImage(filename: "IMG_003.jpg", make: "Nikon", dateTaken: new DateTime(2024, 6, 15))
        };

        // Act - Canon images from 2024 OR Sony images
        var result = FilterService.Apply(images,
            "(exif.make == 'Canon' and year(dateTaken) == 2024) or exif.make == 'Sony'").ToList();

        // Assert
        Assert.HasCount(2, result);
        Assert.IsTrue(result.Any(i => i.Filename == "IMG_001.jpg"));
        Assert.IsTrue(result.Any(i => i.Filename == "DSC_001.jpg"));
    }

    [TestMethod]
    public void Apply_PreservesOrder()
    {
        // Arrange
        var images = new List<ImageContent>
        {
            CreateTestImage(filename: "c.jpg"),
            CreateTestImage(filename: "a.jpg"),
            CreateTestImage(filename: "b.jpg")
        };

        // Act
        var result = FilterService.Apply(images, "contains(filename, '.jpg')").ToList();

        // Assert
        Assert.AreEqual("c.jpg", result[0].Filename);
        Assert.AreEqual("a.jpg", result[1].Filename);
        Assert.AreEqual("b.jpg", result[2].Filename);
    }

    [TestMethod]
    public void Compile_ThrowsOnNullOrEmpty()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() => FilterService.Compile(null!));
        Assert.ThrowsExactly<ArgumentException>(() => FilterService.Compile(""));
        Assert.ThrowsExactly<ArgumentException>(() => FilterService.Compile("   "));
    }

    [TestMethod]
    public void Apply_ThrowsOnNullImages()
    {
        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            FilterService.Apply(null!, "filename == 'test.jpg'"));
    }

    [TestMethod]
    public void Apply_ThrowsOnNullOrEmptyFilter()
    {
        // Arrange
        var images = new List<ImageContent>();

        // Act & Assert
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            FilterService.Apply(images, null!));
        Assert.ThrowsExactly<ArgumentException>(() =>
            FilterService.Apply(images, ""));
    }

    [TestMethod]
    public void CompileToExpression_ReturnsLinqExpression()
    {
        // Act
        var expression = FilterService.CompileToExpression("filename == 'test.jpg'");

        // Assert
        Assert.IsNotNull(expression);
        Assert.IsInstanceOfType<System.Linq.Expressions.LambdaExpression>(expression);
    }

    [TestMethod]
    public void FilterParseException_ContainsDetailedMessage()
    {
        // Act
        FilterService.TryValidate("filename == ", out var error);

        // Assert
        Assert.IsNotNull(error);
        // Should contain the original filter and point to the error position
        Assert.Contains("filename", error);
    }
}
